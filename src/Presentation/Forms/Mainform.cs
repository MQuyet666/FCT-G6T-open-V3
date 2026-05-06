using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using FCT.G6T.Application.Interfaces;
using FCT.G6T.Domain.Interfaces;
using FCT.G6T.Domain.Models;
using FCT.G6T.HAL.Serial;
using FCT.G6T.Presentation.Controls;

namespace FCT.G6T.Presentation.Forms
{
    public partial class Mainform : Form
    {
        private readonly ICameraPreviewAppService _cameraPreviewService;
        private readonly IComPortProvider _comPortProvider;
        private readonly ISmokeDeviceTestService _smokeDeviceTestService;
        private readonly IHeatDeviceTestService _heatDeviceTestService;
        private readonly ITestCaseProvider _testCaseProvider;
    private readonly IG6TAdapter _g6tAdapter;
    private readonly IDetectorAdapter _detectorAdapter;
        private CameraPreviewControl _cameraPreview;
        private readonly Dictionary<string, Label> _testStatusLabels = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Panel> _testStatusLeds = new(StringComparer.OrdinalIgnoreCase);
        private string _currentDeviceType = "smoke";
        private bool _isRunningTest;
        private bool _powerAckPassed;
        private bool _isQrConnected;
        private CancellationTokenSource? _testCts;
        private readonly System.Windows.Forms.Timer _comPortRefreshTimer = new();

        private const int WmDeviceChange = 0x0219;
        private const int DbtDeviceArrival = 0x8000;
        private const int DbtDeviceRemoveComplete = 0x8004;

        public Mainform()
        {
            InitializeComponent();

            _cameraPreviewService = null!;
            _comPortProvider = null!;
            _smokeDeviceTestService = null!;
            _heatDeviceTestService = null!;
            _testCaseProvider = null!;
        _g6tAdapter = null!;
        _detectorAdapter = null!;
            _cameraPreview = null!;
        }

        public Mainform(
            ICameraPreviewAppService cameraPreviewService,
            IComPortProvider comPortProvider,
            ISmokeDeviceTestService smokeDeviceTestService,
            IHeatDeviceTestService heatDeviceTestService,
        ITestCaseProvider testCaseProvider,
        IG6TAdapter g6tAdapter,
        IDetectorAdapter detectorAdapter)
        {
            InitializeComponent();

            _cameraPreviewService = cameraPreviewService;
            _comPortProvider = comPortProvider;
            _smokeDeviceTestService = smokeDeviceTestService;
            _heatDeviceTestService = heatDeviceTestService;
            _testCaseProvider = testCaseProvider;
        _g6tAdapter = g6tAdapter;
        _detectorAdapter = detectorAdapter;

            _cameraPreview = new CameraPreviewControl(_cameraPreviewService);
            _cameraPreview.Dock = DockStyle.Fill;
            _cameraPreview.SetRoi1(new Rectangle(540, 220, 200, 200));
            pnlCamera.Controls.Add(_cameraPreview);

            _comPortRefreshTimer.Interval = 500;
            _comPortRefreshTimer.Tick += ComPortRefreshTimer_Tick;
            Shown += Mainform_Shown;
        }

        private ISmokeDeviceTestService ActiveDeviceTestService =>
            _currentDeviceType.Equals("heat", StringComparison.OrdinalIgnoreCase)
                ? _heatDeviceTestService
                : _smokeDeviceTestService;

        private void btnClear_Click(object sender, EventArgs e)
        {
            txtLog.Clear();
        }

        // ================= START BUTTON =================
        private async void btnStart_Click(object sender, EventArgs e)
        {
            try
            {
                if (_isRunningTest)
                {
                    return;
                }

                if (!_currentDeviceType.Equals("smoke", StringComparison.OrdinalIgnoreCase) &&
                    !_currentDeviceType.Equals("heat", StringComparison.OrdinalIgnoreCase))
                {
                    AppendLog("Chi ho tro flow START cho dau bao khoi (Smoke) va dau bao nhiet (Heat).");
                    return;
                }

                if (cbG6tCom.SelectedItem is not string g6tComPort || string.IsNullOrWhiteSpace(g6tComPort))
                {
                    AppendLog("Vui long chon G6T COM truoc khi chay test.");
                    return;
                }

                if (cbDiCom.SelectedItem is not string detectorComPort || string.IsNullOrWhiteSpace(detectorComPort))
                {
                    AppendLog("Vui long chon DT COM truoc khi chay test.");
                    return;
                }

                _isRunningTest = true;
                btnStart.Enabled = false;
                _testCts?.Cancel();
                _testCts?.Dispose();
                _testCts = new CancellationTokenSource();
                var ct = _testCts.Token;
                _cameraPreview.SetRoi1Detected(false);
                foreach (var testName in GetCurrentDeviceTestNames())
                {
                    SetTestRunning(testName);
                }

                // reset ACK trackers
                _powerAckPassed = false;

                AppendLog($"=== START TEST: {_currentDeviceType.ToUpperInvariant()} ===");
                var roi1 = _cameraPreview.GetRoi1SourceRect();
                var progress = new Progress<string>(HandleProgressLog);
                var stepResults = await ActiveDeviceTestService.RunStartSequenceAsync(g6tComPort, detectorComPort, roi1, _currentDeviceType, progress, ct).ConfigureAwait(true);

                var finalStepResults = stepResults
                    .GroupBy(step => step.StepName, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.Last())
                    .ToList();

                var allPassed = finalStepResults.All(step => step.IsPassed);
                if (!allPassed)
                {
                    var firstFailedStep = finalStepResults.FirstOrDefault(step => !step.IsPassed);
                    if (firstFailedStep is not null)
                    {
                        AppendLog($"[RESULT][FAIL] Buoc loi: {firstFailedStep.StepName} - {ExtractFailReason(firstFailedStep.Message)}");
                    }
                }

                WriteResultFile(finalStepResults, allPassed);
                AppendLog(allPassed ? "=== TEST FLOW HOAN TAT ===" : "=== TEST FLOW DUNG DO FAIL ===");
                // After UI shows final result, explicitly call Reset sequence to ensure TX occurs after result
                try
                {
                    await Task.Delay(200).ConfigureAwait(true); // give UI moment to render
                    await ActiveDeviceTestService.SendResetAsync(g6tComPort, new Progress<string>(AppendLog), ct).ConfigureAwait(true);
                }
                catch (TimeoutException ex)
                {
                    AppendLog($"[ERROR] Gui reset that bai: {ex.Message}");
                }
                catch (InvalidOperationException ex)
                {
                    AppendLog($"[ERROR] Gui reset that bai: {ex.Message}");
                }
                catch (InvalidDataException ex)
                {
                    AppendLog($"[ERROR] Gui reset that bai: {ex.Message}");
                }
                catch (OperationCanceledException)
                {
                    AppendLog("[INFO] Test bi huy.");
                }
            }
            catch (ArgumentException ex)
            {
                AppendLog($"[ERROR] {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                AppendLog($"[ERROR] {ex.Message}");
            }
            catch (FCT.G6T.HAL.Serial.HardwareException ex)
            {
                AppendLog($"[ERROR] {ex.Message}");
            }
            catch (OperationCanceledException)
            {
                AppendLog("[INFO] Test bi huy.");
            }
            finally
            {
                _isRunningTest = false;
                btnStart.Enabled = true;
                _testCts?.Dispose();
                _testCts = null;
            }
        }

        private void StartCameraPreview()
        {
            if (_cameraPreviewService.IsRunning) return;

            _cameraPreview.StartPreview();
            //btnStart.Enabled = false;
            //btnStop.Enabled = true;   // n?u c� btnStop
        }

        // ================= STOP (n?u c� btnStop) =================
        //private void btnStop_Click(object sender, EventArgs e)
        //{
        //    _cameraPreview.StopPreview();
        //    btnStart.Enabled = true;
        //    btnStop.Enabled = false;
        //}

        private void Mainform_Shown(object? sender, EventArgs e)
        {
            if (_cameraPreview is not null)
            {
                _cameraPreview.BringToFront();
            }

            if (!_cameraPreviewService.IsRunning)
            {
                StartCameraPreview();
            }
        }

        // ================= LOG =================
        private void txtLog_TextChanged(object sender, EventArgs e) { }

        private void lblStatus_Click(object sender, EventArgs e) { }

        private void lblSerial_Click(object sender, EventArgs e) { }

        private void cbDiCom_SelectedIndexChanged(object sender, EventArgs e) { }

        private void cbQrCom_SelectedIndexChanged(object sender, EventArgs e) { }

        private void label1_Click(object sender, EventArgs e) { }

        private void label3_Click(object sender, EventArgs e) { }

        private void radioButton1_CheckedChanged(object sender, EventArgs e) { }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton2.Checked)
            {
                _currentDeviceType = "smoke";
                ShowTestCasesForDevice(_currentDeviceType);
            }
        }

        private void radioButton3_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton3.Checked)
            {
                _currentDeviceType = "heat";
                ShowTestCasesForDevice(_currentDeviceType);
            }
        }

        private void radioButton4_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton4.Checked)
            {
                _currentDeviceType = "bell";
                ShowTestCasesForDevice(_currentDeviceType);
            }
        }

        private void radioButton5_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton5.Checked)
            {
                _currentDeviceType = "button";
                ShowTestCasesForDevice(_currentDeviceType);
            }
        }

        private void ShowTestCasesForDevice(string deviceType)
        {
            grpTest.Controls.Clear();
            _testStatusLabels.Clear();
            _testStatusLeds.Clear();
            grpTest.Text = $"Test Results - {deviceType.ToUpperInvariant()}";

            var testCases = _testCaseProvider.GetTestCasesForDevice(deviceType);
            int index = 0;
            foreach (var test in testCases)
            {
                var y = 25 + index * 28;

                var nameLabel = new Label
                {
                    Text = test.Name,
                    Location = new Point(20, y),
                    AutoSize = true,
                };

                var led = new Panel
                {
                    BackColor = Color.Gray,
                    Size = new Size(15, 15),
                    Location = new Point(250, y),
                    Name = $"led_{index}",
                };

                var statusLabel = new Label
                {
                    Text = "PENDING",
                    ForeColor = Color.DimGray,
                    Location = new Point(280, y - 2),
                    AutoSize = true,
                    Name = $"lblStatus_{index}",
                };

                grpTest.Controls.Add(nameLabel);
                grpTest.Controls.Add(led);
                grpTest.Controls.Add(statusLabel);

                _testStatusLeds[test.Name] = led;
                _testStatusLabels[test.Name] = statusLabel;

                index++;
            }
        }

        private IReadOnlyList<string> GetCurrentDeviceTestNames()
        {
            return _testCaseProvider
                .GetTestCasesForDevice(_currentDeviceType)
                .Select(test => test.Name)
                .ToList();
        }

        // ================= CLEANUP =================
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _g6tAdapter.Trace -= OnG6TTrace;
            _detectorAdapter.Trace -= OnDetectorTrace;
            _testCts?.Cancel();
            _testCts?.Dispose();
            _testCts = null;
            _comPortRefreshTimer.Stop();
            _comPortRefreshTimer.Dispose();
            _cameraPreview?.StopPreview();
            (_cameraPreviewService as IDisposable)?.Dispose();
            base.OnFormClosing(e);
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void cbG6tCom_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void Mainform_Load(object sender, EventArgs e)
        {
            LoadComPorts();
            ShowTestCasesForDevice(_currentDeviceType);
            UpdateConnectButtonText();
            UpdateDetectorConnectButtonText();
            UpdateQrConnectButtonText();
        _g6tAdapter.Trace += OnG6TTrace;
        _detectorAdapter.Trace += OnDetectorTrace;
        }

    private void OnG6TTrace(object? sender, G6TTraceEventArgs e)
    {
        AppendLog(e.Message);
    }

    private void OnDetectorTrace(object? sender, DetectorTraceEventArgs e)
    {
        AppendLog(e.Message);
    }

        private void LoadComPorts()
        {
            var selectedQrPort = comboBox1.SelectedItem as string;
            var selectedDetectorPort = cbDiCom.SelectedItem as string;
            var selectedG6tPort = cbG6tCom.SelectedItem as string;

            var ports = _comPortProvider.GetAvailableComPorts();

            comboBox1.Items.Clear();
            cbDiCom.Items.Clear();
            cbG6tCom.Items.Clear();

            foreach (var port in ports)
            {
                comboBox1.Items.Add(port); // QR COM
                cbDiCom.Items.Add(port);   // DT COM
                cbG6tCom.Items.Add(port);  // G6T COM
            }

            RestoreSelectedPort(comboBox1, selectedQrPort);
            RestoreSelectedPort(cbDiCom, selectedDetectorPort);
            RestoreSelectedPort(cbG6tCom, selectedG6tPort);
        }

        private static void RestoreSelectedPort(ComboBox comboBox, string? previousPort)
        {
            if (!string.IsNullOrWhiteSpace(previousPort) && comboBox.Items.Contains(previousPort))
            {
                comboBox.SelectedItem = previousPort;
                return;
            }

            comboBox.SelectedIndex = comboBox.Items.Count > 0 ? 0 : -1;
        }

        private void ComPortRefreshTimer_Tick(object? sender, EventArgs e)
        {
            _comPortRefreshTimer.Stop();
            LoadComPorts();
            AppendLog("[INFO] Da cap nhat danh sach cong COM.");
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg != WmDeviceChange)
            {
                return;
            }

            var eventType = m.WParam.ToInt32();
            if (eventType is DbtDeviceArrival or DbtDeviceRemoveComplete)
            {
                _comPortRefreshTimer.Stop();
                _comPortRefreshTimer.Start();
            }
        }

        private void grpTest_Enter(object sender, EventArgs e)
        {

        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void radioButton4_CheckedChanged_1(object sender, EventArgs e)
        {

        }

        private void radioButton5_CheckedChanged_1(object sender, EventArgs e)
        {

        }

        private void pnlCamera_Paint(object sender, PaintEventArgs e)
        {

        }

        private void AppendLog(string message)
        {
        if (txtLog.InvokeRequired)
        {
            txtLog.Invoke(() => AppendLog(message));
            return;
        }

        var lines = message.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
        foreach (var line in lines)
        {
            txtLog.AppendText($"{DateTime.Now:HH:mm:ss} {line}{Environment.NewLine}");
        }

        txtLog.ScrollToCaret();
        }

        private void HandleProgressLog(string message)
        {
            if (message.Equals("[ROI1][RESET]", StringComparison.OrdinalIgnoreCase))
            {
                _cameraPreview.SetRoi1Detected(false);
                return;
            }

            if (message.Equals("[ROI1][PASS]", StringComparison.OrdinalIgnoreCase))
            {
                _cameraPreview.SetRoi1Detected(true);
                return;
            }

            // Update test result indicators based on realtime progress messages
            if (message.StartsWith("[SENDING]", StringComparison.OrdinalIgnoreCase) ||
                message.StartsWith("[WAITING]", StringComparison.OrdinalIgnoreCase))
            {
                // format: [SENDING] <StepName> -> <Port>
                // extract full step name between the bracket and '->' to support multi-word names
                var idx = message.IndexOf(']');
                if (idx >= 0 && idx + 1 < message.Length)
                {
                    var after = message[(idx + 1)..].Trim();
                    var arrowIdx = after.IndexOf("->", StringComparison.Ordinal);
                    var stepNameFull = arrowIdx >= 0 ? after.Substring(0, arrowIdx).Trim() : after;

                    if (stepNameFull.IndexOf("LED", StringComparison.OrdinalIgnoreCase) >= 0 || message.Contains("LED", StringComparison.OrdinalIgnoreCase))
                    {
                        SetTestRunning("LED Test");
                    }

                    if (stepNameFull.IndexOf("Button", StringComparison.OrdinalIgnoreCase) >= 0 || message.Contains("Button", StringComparison.OrdinalIgnoreCase))
                    {
                        SetTestRunning("Button Test");
                    }

                    if (stepNameFull.IndexOf("Lora", StringComparison.OrdinalIgnoreCase) >= 0 || message.Contains("Lora", StringComparison.OrdinalIgnoreCase))
                    {
                        SetTestRunning("Lora Test");
                    }

                    if (stepNameFull.IndexOf("Read Value", StringComparison.OrdinalIgnoreCase) >= 0 || message.Contains("Read Value", StringComparison.OrdinalIgnoreCase))
                    {
                        SetTestRunning("Read Value Test");
                    }
                }
            }

            // Track ACK status for prerequisite: Power
            if (message.Contains("[ACK][PASS]", StringComparison.OrdinalIgnoreCase))
            {
                if (message.Contains("C?p ngu?n", StringComparison.OrdinalIgnoreCase) || message.Contains("PowerControl", StringComparison.OrdinalIgnoreCase))
                {
                    _powerAckPassed = true;
                }
            }

            if (message.Contains("[ACK][FAIL]", StringComparison.OrdinalIgnoreCase))
            {
                if (message.Contains("C?p ngu?n", StringComparison.OrdinalIgnoreCase) || message.Contains("PowerControl", StringComparison.OrdinalIgnoreCase))
                {
                    _powerAckPassed = false;
                }
            }

            if (message.Contains("LED Test", StringComparison.OrdinalIgnoreCase) && message.Contains("[ACK][PASS]", StringComparison.OrdinalIgnoreCase))
            {
                SetTestPassed("LED Test");
            }

            if (message.Contains("LED Test", StringComparison.OrdinalIgnoreCase) && message.Contains("[ACK][FAIL]", StringComparison.OrdinalIgnoreCase))
            {
                SetTestFailed("LED Test");
            }

            // LED Test result: only change indicator if Power ACK is successful
            if ((message.Contains("[ROI1][PASS]", StringComparison.OrdinalIgnoreCase) || (message.Contains("LED ROI Detect", StringComparison.OrdinalIgnoreCase) && message.Contains("[ACK][PASS]", StringComparison.OrdinalIgnoreCase)))
                && _powerAckPassed)
            {
                SetTestPassed("LED Test");
            }

            if ((message.Contains("[ACK][FAIL] LED", StringComparison.OrdinalIgnoreCase) || (message.Contains("LED ROI Detect", StringComparison.OrdinalIgnoreCase) && message.Contains("[ACK][FAIL]", StringComparison.OrdinalIgnoreCase)))
                && _powerAckPassed)
            {
                SetTestFailed("LED Test");
            }

            if (message.Contains("[ACK][PASS] Button", StringComparison.OrdinalIgnoreCase) || message.Contains("Button Test", StringComparison.OrdinalIgnoreCase) && message.Contains("[ACK][PASS]"))
            {
                SetTestPassed("Button Test");
            }

            if (message.Contains("[ACK][FAIL] Button", StringComparison.OrdinalIgnoreCase) || message.Contains("Button Test", StringComparison.OrdinalIgnoreCase) && message.Contains("[ACK][FAIL]"))
            {
                SetTestFailed("Button Test");
            }

            if (message.Contains("[ACK][PASS] Lora", StringComparison.OrdinalIgnoreCase) || message.Contains("Lora Test", StringComparison.OrdinalIgnoreCase) && message.Contains("[ACK][PASS]"))
            {
                SetTestPassed("Lora Test");
            }

            if (message.Contains("[ACK][FAIL] Lora", StringComparison.OrdinalIgnoreCase) || message.Contains("Lora Test", StringComparison.OrdinalIgnoreCase) && message.Contains("[ACK][FAIL]"))
            {
                SetTestFailed("Lora Test");
            }

            if (message.Contains("[ACK][PASS] Read Value", StringComparison.OrdinalIgnoreCase) || message.Contains("Read Value Test", StringComparison.OrdinalIgnoreCase) && message.Contains("[ACK][PASS]"))
            {
                SetTestPassed("Read Value Test");
            }

            if (message.Contains("[ACK][FAIL] Read Value", StringComparison.OrdinalIgnoreCase) || message.Contains("Read Value Test", StringComparison.OrdinalIgnoreCase) && message.Contains("[ACK][FAIL]"))
            {
                SetTestFailed("Read Value Test");
            }

            AppendLog(message);
        }

        private void UpdateConnectButtonText()
        {
            button2.Text = ActiveDeviceTestService.IsConnected ? "G6T Disconnect" : "G6T Connect";
        }

        private void UpdateDetectorConnectButtonText()
        {
            button3.Text = ActiveDeviceTestService.IsDetectorConnected ? "DT Disconnect" : "DT Connect";
        }
        private void UpdateQrConnectButtonText()
        {
            button1.Text = _isQrConnected ? "QR Disconnect" : "QR Connect";
        }
        private static string ExtractFailReason(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "Kh�ng c� th�ng tin chi ti?t.";
            }

            var lines = message.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            var ackFailLine = lines.FirstOrDefault(line => line.Contains("[ACK][FAIL]", StringComparison.OrdinalIgnoreCase));
            return ackFailLine ?? lines[^1];
        }

        private void WriteResultFile(IReadOnlyList<TestStepResult> finalStepResults, bool allPassed)
        {
            var serial = string.IsNullOrWhiteSpace(txtSerial.Text) ? "N/A" : txtSerial.Text.Trim();
            var failedStep = finalStepResults.FirstOrDefault(step => !step.IsPassed);
            var resultPath = Path.Combine(AppContext.BaseDirectory, "Result.txt");

            var builder = new StringBuilder();
            builder.AppendLine($"Serial: {serial}");
            builder.AppendLine($"Station: {GetStationName(_currentDeviceType)}");
            builder.AppendLine($"DateTime: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine($"Result: {(allPassed ? "PASS" : "FAIL")}");

            if (!allPassed && failedStep is not null)
            {
                builder.AppendLine($"Error: {failedStep.StepName} failed. ({ExtractFailReason(failedStep.Message)})");

                var frameLog = ExtractTxFrameLog(failedStep);
                if (!string.IsNullOrWhiteSpace(frameLog))
                {
                    builder.AppendLine($"Log: {frameLog}");
                }
            }

            builder.AppendLine("//*******************************END TEST**********************************//");
            File.AppendAllText(resultPath, builder.ToString(), Encoding.UTF8);
            AppendLog($"[INFO] Da ghi ket qua vao {resultPath}");
        }

        private static string GetStationName(string deviceType)
        {
            return deviceType.ToLowerInvariant() switch
            {
                "smoke" => "Đầu báo khói",
                "heat" => "Đầu báo nhiệt",
                "bell" => "Đèn chuông",
                "button" => "Nút nhấn",
                _ => deviceType,
            };
        }

        private static string ExtractTxFrameLog(TestStepResult stepResult)
        {
            if (string.IsNullOrWhiteSpace(stepResult.Message))
            {
                return string.Empty;
            }

            var lines = stepResult.Message.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            var txLine = lines.LastOrDefault(line => line.TrimStart().StartsWith("[TX]", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(txLine))
            {
                return string.Empty;
            }

            var line = txLine.Trim();
            var payloadStart = line.IndexOf(']');
            if (payloadStart < 0 || payloadStart + 1 >= line.Length)
            {
                return line;
            }

            var destination = "UNKNOWN";
            var secondBracketStart = line.IndexOf('[', payloadStart + 1);
            var secondBracketEnd = secondBracketStart >= 0 ? line.IndexOf(']', secondBracketStart + 1) : -1;
            if (secondBracketStart >= 0 && secondBracketEnd > secondBracketStart)
            {
                destination = line.Substring(secondBracketStart + 1, secondBracketEnd - secondBracketStart - 1);
                payloadStart = secondBracketEnd;
            }

            var frame = line[(payloadStart + 1)..].Trim();
            return $"{stepResult.StepName} -> {destination}: {frame}";
        }

        private void SetTestRunning(string testName)
        {
            if (_testStatusLeds.TryGetValue(testName, out var led))
            {
                led.BackColor = Color.Gray;
            }

            if (_testStatusLabels.TryGetValue(testName, out var status))
            {
                status.Text = "RUNNING";
                status.ForeColor = Color.DimGray;
            }
        }

        private async void button3_Click(object sender, EventArgs e)
        {
            if (cbDiCom.SelectedItem is not string detectorComPort || string.IsNullOrWhiteSpace(detectorComPort))
            {
                AppendLog("Vui lòng chọn DT COM trước khi connect.");
                return;
            }

            try
            {
                button3.Enabled = false;

                if (ActiveDeviceTestService.IsDetectorConnected)
                {
                    await ActiveDeviceTestService.DisconnectDetectorAsync().ConfigureAwait(true);
                    AppendLog($"Disconnect DT COM: {detectorComPort}");
                }
                else
                {
                    await ActiveDeviceTestService.ConnectDetectorAsync(detectorComPort).ConfigureAwait(true);
                    AppendLog($"Connect DT COM: {detectorComPort} @ 9600");
                }

                UpdateDetectorConnectButtonText();
            }
            catch (ArgumentException ex)
            {
                AppendLog($"[ERROR] {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                AppendLog($"[ERROR] {ex.Message}");
            }
            catch (HardwareException ex)
            {
                AppendLog($"[ERROR] {ex.Message}");
            }
            catch (UnauthorizedAccessException)
            {
                AppendLog($"[ERROR] COM {detectorComPort} dang bi chiem. Hay dong ung dung khac dang su dung cong.");
            }
            catch (Exception ex)
            {
                AppendLog($"[ERROR] Connect DT COM that bai: {ex.Message}");
            }
            finally
            {
                button3.Enabled = true;
                UpdateDetectorConnectButtonText();
            }
        }

        private void SetTestPassed(string testName)
        {
            if (_testStatusLeds.TryGetValue(testName, out var led))
            {
                led.BackColor = Color.Lime;
            }

            if (_testStatusLabels.TryGetValue(testName, out var status))
            {
                status.Text = "PASS";
                status.ForeColor = Color.Green;
            }
        }

        private void SetTestFailed(string testName)
        {
            if (_testStatusLeds.TryGetValue(testName, out var led))
            {
                led.BackColor = Color.Red;
            }

            if (_testStatusLabels.TryGetValue(testName, out var status))
            {
                status.Text = "FAIL";
                status.ForeColor = Color.Red;
            }
        }

        private void txtSerial_TextChanged(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (comboBox1.SelectedItem is not string qrComPort || string.IsNullOrWhiteSpace(qrComPort))
            {
                AppendLog("Vui lòng chọn QR COM trước khi connect.");
                return;
            }

            if (_isQrConnected)
            {
                _isQrConnected = false;
                AppendLog($"Disconnect QR COM: {qrComPort}");
            }
            else
            {
                _isQrConnected = true;
                AppendLog($"Connect QR COM: {qrComPort}");
            }

            UpdateQrConnectButtonText();
        }

        private async void button2_Click(object sender, EventArgs e)
        {
            try
            {
                if (cbG6tCom.SelectedItem is not string g6tComPort || string.IsNullOrWhiteSpace(g6tComPort))
                {
                    AppendLog("Vui long chon G6T COM truoc khi connect.");
                    return;
                }

                if (ActiveDeviceTestService.IsConnected)
                {
                    await ActiveDeviceTestService.DisconnectAsync().ConfigureAwait(true);
                    AppendLog($"Disconnect G6T COM: {g6tComPort}");
                }
                else
                {
                    await ActiveDeviceTestService.ConnectAsync(g6tComPort).ConfigureAwait(true);
                    AppendLog($"Connect G6T COM: {g6tComPort}");
                    using var connectCts = new CancellationTokenSource();
                    await ActiveDeviceTestService.PrepareOnConnectAsync(g6tComPort, new Progress<string>(HandleProgressLog), connectCts.Token).ConfigureAwait(true);
                }

                UpdateConnectButtonText();
            }
            catch (ArgumentException ex)
            {
                AppendLog($"[ERROR] {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                AppendLog($"[ERROR] {ex.Message}");
            }
            catch (HardwareException ex)
            {
                AppendLog($"[ERROR] {ex.Message}");
            }
            catch (OperationCanceledException)
            {
                AppendLog("[INFO] Connect bi huy.");
            }
        }
    }
}
