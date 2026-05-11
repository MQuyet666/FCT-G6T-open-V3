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
        private readonly IQrCodeScanService _qrCodeScanService;
        private CameraPreviewControl _cameraPreview;
        private readonly Dictionary<string, Label> _testStatusLabels = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Panel> _testStatusLeds = new(StringComparer.OrdinalIgnoreCase);
        private string _currentDeviceType = "smoke";
        private bool _isRunningTest;
        private bool _powerAckPassed;
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
            _qrCodeScanService = null!;
            _cameraPreview = null!;
        }

        public Mainform(
            ICameraPreviewAppService cameraPreviewService,
            IComPortProvider comPortProvider,
            ISmokeDeviceTestService smokeDeviceTestService,
            IHeatDeviceTestService heatDeviceTestService,
            ITestCaseProvider testCaseProvider,
            IG6TAdapter g6tAdapter,
            IDetectorAdapter detectorAdapter,
            IQrCodeScanService qrCodeScanService)
        {
            InitializeComponent();

            _cameraPreviewService = cameraPreviewService;
            _comPortProvider = comPortProvider;
            _smokeDeviceTestService = smokeDeviceTestService;
            _heatDeviceTestService = heatDeviceTestService;
            _testCaseProvider = testCaseProvider;
            _g6tAdapter = g6tAdapter;
            _detectorAdapter = detectorAdapter;
            _qrCodeScanService = qrCodeScanService;

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

        private QrScanMode CurrentQrScanMode => radioButton7.Checked ? QrScanMode.Use : QrScanMode.NoUse;

        private void UpdateSerialInputState()
        {
            var qrUse = CurrentQrScanMode == QrScanMode.Use;
            txtSerial.ReadOnly = true;
            txtSerial.Enabled = qrUse;
            if (!qrUse)
            {
                txtSerial.Clear();
            }
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

                if (radioButton7.Checked && (comboBox1.SelectedItem is not string qrComPort || string.IsNullOrWhiteSpace(qrComPort)))
                {
                    AppendLog("Vui long chon QR COM truoc khi chay test.");
                    return;
                }

                _isRunningTest = true;
                btnStart.Enabled = false;
                _testCts?.Cancel();
                _testCts?.Dispose();
                _testCts = new CancellationTokenSource();
                var ct = _testCts.Token;
                txtSerial.Clear();
                _cameraPreview.SetRoi1Detected(false);
                foreach (var testName in GetCurrentDeviceTestNames())
                {
                    SetTestRunning(testName);
                }

                // reset ACK trackers
                _powerAckPassed = false;

                AppendLog($"=== START TEST: {_currentDeviceType.ToUpperInvariant()} ===");
                if (radioButton7.Checked && comboBox1.SelectedItem is string selectedQrComPort)
                {
                    var qrPassed = await ScanQrToSerialAsync(selectedQrComPort, ct).ConfigureAwait(true);
                    if (!qrPassed)
                    {
                        var qrFailResults = BuildQrFailResults();
                        foreach (var testName in GetCurrentDeviceTestNames())
                        {
                            SetTestFailed(testName);
                        }

                        AppendLog("[RESULT][FAIL] QR Scan - Khong nhan duoc data QR trong 5s.");
                        AppendLog("=== TEST FLOW DUNG DO FAIL ===");
                        WriteDeviceLogFile(qrFailResults, Array.Empty<TestStepResult>(), allPassed: false);
                        return;
                    }
                }

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

                AppendLog(allPassed ? "=== TEST FLOW HOAN TAT ===" : "=== TEST FLOW DUNG DO FAIL ===");
                IReadOnlyList<TestStepResult> restoreStepResults = Array.Empty<TestStepResult>();
                // After UI shows final result, explicitly call Reset sequence to ensure TX occurs after result
                try
                {
                    await Task.Delay(200).ConfigureAwait(true); // give UI moment to render
                    restoreStepResults = await ActiveDeviceTestService.SendResetAsync(g6tComPort, new Progress<string>(AppendLog), ct).ConfigureAwait(true);
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

                WriteDeviceLogFile(finalStepResults, restoreStepResults, allPassed);
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
            UpdateSerialInputState();
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
            button1.Text = _qrCodeScanService.IsConnected ? "QR Disconnect" : "QR Connect";
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

        private void WriteDeviceLogFile(
            IReadOnlyList<TestStepResult> finalStepResults,
            IReadOnlyList<TestStepResult> restoreStepResults,
            bool allPassed)
        {
            var serial = string.IsNullOrWhiteSpace(txtSerial.Text) ? "N/A" : txtSerial.Text.Trim();
            var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logDirectory);

            var resultSuffix = allPassed ? "pass" : "fail";
            var resultPath = Path.Combine(logDirectory, $"device-{NormalizeDeviceType(_currentDeviceType)}-{resultSuffix}.txt");
            var builder = new StringBuilder();
            builder.AppendLine("//***********************************START TEST**********************************//");
            builder.AppendLine();
            builder.AppendLine($"DEVICE NAME : {GetDeviceLogName(_currentDeviceType)}");
            builder.AppendLine($"SERIAL      : {serial}");
            builder.AppendLine($"DATE TIME   : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine();

            if (TryAppendQrFailLog(builder, finalStepResults, allPassed))
            {
                File.AppendAllText(resultPath, builder.ToString(), Encoding.UTF8);
                AppendLog($"[INFO] Da ghi log thiet bi vao {resultPath}");
                return;
            }

            AppendPowerOnLog(builder, finalStepResults);
            AppendCommandStepLog(builder, finalStepResults, "Button Test", "Button Test", "G6T");
            AppendCommandStepLog(builder, finalStepResults, "UART On", "UART ON", "G6T");
            AppendCommandStepLog(builder, finalStepResults, "Calib Set ON", "Calib Set ON", "G6T");

            if (HasAnyStep(finalStepResults, "Lora Test", "Read Value Test"))
            {
                builder.AppendLine("[STEP] DT COM Connected");
                builder.AppendLine();
            }

            AppendDetectorStepLog(builder, finalStepResults, "Lora Test", includeValue: false);
            AppendDetectorStepLog(builder, finalStepResults, "Read Value Test", includeValue: true);

            builder.AppendLine("[FINAL RESULT]");
            builder.AppendLine($"TOTAL TEST : {(allPassed ? "PASS" : "FAIL")}");
            builder.AppendLine();

            AppendRestoreStateLog(builder, restoreStepResults);
            builder.AppendLine("//***********************************END TEST***********************************//");

            File.AppendAllText(resultPath, builder.ToString(), Encoding.UTF8);
            AppendLog($"[INFO] Da ghi log thiet bi vao {resultPath}");
        }

        private IReadOnlyList<TestStepResult> BuildQrFailResults()
        {
            const string message = "[ACK][FAIL] QR Scan - Khong nhan duoc data QR trong 5s.";
            var results = new List<TestStepResult>
            {
                new()
                {
                    StepName = "QR Scan",
                    IsPassed = false,
                    Message = message,
                }
            };

            results.AddRange(GetCurrentDeviceTestNames().Select(testName => new TestStepResult
            {
                StepName = testName,
                IsPassed = false,
                Message = message,
            }));

            return results;
        }

        private static bool TryAppendQrFailLog(StringBuilder builder, IReadOnlyList<TestStepResult> finalStepResults, bool allPassed)
        {
            var qrFail = finalStepResults.FirstOrDefault(step =>
                step.StepName.Equals("QR Scan", StringComparison.OrdinalIgnoreCase) && !step.IsPassed);
            if (qrFail is null)
            {
                return false;
            }

            builder.AppendLine("[STEP] QR Scan");
            builder.AppendLine("RESULT     : FAIL");
            builder.AppendLine($"ERROR      : {ExtractFailReason(qrFail.Message)}");
            builder.AppendLine();

            builder.AppendLine("[TEST STATUS]");
            foreach (var step in finalStepResults.Where(step => !step.StepName.Equals("QR Scan", StringComparison.OrdinalIgnoreCase)))
            {
                builder.AppendLine($"- {step.StepName} : FAIL");
            }

            builder.AppendLine();
            builder.AppendLine("[FINAL RESULT]");
            builder.AppendLine($"TOTAL TEST : {(allPassed ? "PASS" : "FAIL")}");
            builder.AppendLine();
            builder.AppendLine("[STEP] Restore State");
            builder.AppendLine("SKIPPED     : QR Scan FAIL, test flow not started");
            builder.AppendLine();
            builder.AppendLine("//***********************************END TEST***********************************//");
            return true;
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

        private static void AppendPowerOnLog(StringBuilder builder, IReadOnlyList<TestStepResult> results)
        {
            var powerResult = results.LastOrDefault(step =>
                !step.StepName.StartsWith("Reset", StringComparison.OrdinalIgnoreCase) &&
                step.Message.Contains("Command=PowerControl", StringComparison.OrdinalIgnoreCase));
            var ledResult = results.LastOrDefault(step =>
                step.StepName.Equals("LED ROI Detect", StringComparison.OrdinalIgnoreCase));
            var tx = ExtractFrame(powerResult?.Message ?? string.Empty, "[TX]");
            var rx = ExtractFrame(powerResult?.Message ?? string.Empty, "[RX]");

            builder.AppendLine("[STEP] Power ON");
            builder.AppendLine($"TX G6T COM : {FormatFrame(tx.Frame)}");
            builder.AppendLine($"RX G6T COM : {FormatFrame(rx.Frame)}");
            builder.AppendLine($"- G6T ACK        : {FormatStatus(powerResult)}");
            builder.AppendLine($"- LED ROI Detect : {FormatLedRoiStatus(ledResult)}");
            builder.AppendLine();
        }

        private static void AppendCommandStepLog(
            StringBuilder builder,
            IReadOnlyList<TestStepResult> results,
            string stepName,
            string displayName,
            string portLabel)
        {
            var result = results.LastOrDefault(step => step.StepName.Equals(stepName, StringComparison.OrdinalIgnoreCase));
            if (result is null)
            {
                return;
            }

            var tx = ExtractFrame(result.Message, "[TX]");
            var rx = ExtractFrame(result.Message, "[RX]");

            builder.AppendLine($"[STEP] {displayName}");
            builder.AppendLine($"TX {portLabel} COM : {FormatFrame(tx.Frame)}");
            builder.AppendLine($"RX {portLabel} COM : {FormatFrame(rx.Frame)}");
            builder.AppendLine($"RESULT     : {FormatStatus(result)}");
            builder.AppendLine();
        }

        private static void AppendDetectorStepLog(
            StringBuilder builder,
            IReadOnlyList<TestStepResult> results,
            string stepName,
            bool includeValue)
        {
            var result = results.LastOrDefault(step => step.StepName.Equals(stepName, StringComparison.OrdinalIgnoreCase));
            if (result is null)
            {
                return;
            }

            var tx = ExtractFrame(result.Message, "[TX]");
            var rx = ExtractFrame(result.Message, "[RX]");
            var rxTraceLines = ExtractDetectorRxTraceLines(result.Message);

            builder.AppendLine($"[STEP] {stepName}");
            builder.AppendLine($"TX DT COM : {FormatFrame(tx.Frame)}");
            if (rxTraceLines.Count > 0)
            {
                foreach (var rxTraceLine in rxTraceLines)
                {
                    builder.AppendLine($"RX DT COM : {rxTraceLine}");
                }
            }
            else
            {
                builder.AppendLine($"RX DT COM : {FormatFrame(rx.Frame)}");
            }

            if (includeValue)
            {
                builder.AppendLine($"VALUE     : {ExtractValue(result.Message)}");
            }

            builder.AppendLine($"RESULT    : {FormatStatus(result)}");
            builder.AppendLine();
        }

        private static void AppendRestoreStateLog(StringBuilder builder, IReadOnlyList<TestStepResult> restoreResults)
        {
            builder.AppendLine("[STEP] Restore State");
            builder.AppendLine("ACK TIMEOUT : 2s");
            builder.AppendLine("RETRY       : 1");
            builder.AppendLine();
            AppendRestoreCommandLog(builder, restoreResults, "Reset PowerOff", "Power OFF");
            AppendRestoreCommandLog(builder, restoreResults, "Reset SetCalibPinOff", "Calib Set OFF");
            AppendRestoreCommandLog(builder, restoreResults, "Reset UartOff", "UART OFF");
            builder.AppendLine();
        }

        private static void AppendRestoreCommandLog(
            StringBuilder builder,
            IReadOnlyList<TestStepResult> restoreResults,
            string stepName,
            string displayName)
        {
            var attempts = restoreResults
                .Where(step => step.StepName.Equals(stepName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (attempts.Count == 0)
            {
                builder.AppendLine($"- {displayName} : N/A");
                return;
            }

            builder.AppendLine($"- {displayName}");
            for (var i = 0; i < attempts.Count; i++)
            {
                var attempt = attempts[i];
                var tx = ExtractFrame(attempt.Message, "[TX]");
                var rx = ExtractFrame(attempt.Message, "[RX]");
                var suffix = attempts.Count > 1 ? $" (attempt {i + 1})" : string.Empty;

                builder.AppendLine($"  TX G6T COM : {FormatFrame(tx.Frame)}{suffix}");
                builder.AppendLine($"  RX G6T COM : {FormatFrame(rx.Frame)}{suffix}");
                builder.AppendLine($"  RESULT     : {FormatStatus(attempt)}{suffix}");
            }
        }

        private static TestStepResult? FindStep(IReadOnlyList<TestStepResult> results, string stepName)
        {
            return results.LastOrDefault(step => step.StepName.Equals(stepName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasAnyStep(IReadOnlyList<TestStepResult> results, params string[] stepNames)
        {
            return results.Any(step => stepNames.Any(name => step.StepName.Equals(name, StringComparison.OrdinalIgnoreCase)));
        }

        private static string FormatStatus(TestStepResult? result)
        {
            return result is null ? "N/A" : result.IsPassed ? "PASS" : "FAIL";
        }

        private static string FormatLedRoiStatus(TestStepResult? result)
        {
            if (result is null)
            {
                return "N/A";
            }

            return result.IsPassed ? "PASS (Detected Red + Green LED)" : $"FAIL ({GetLedRoiFailReason(result.Message)})";
        }

        private static string GetLedRoiFailReason(string message)
        {
            if (message.Contains("Missing=Red,Green", StringComparison.OrdinalIgnoreCase))
            {
                return "Missing Red + Green LED";
            }

            if (message.Contains("Missing=Red", StringComparison.OrdinalIgnoreCase))
            {
                return "Missing Red LED";
            }

            if (message.Contains("Missing=Green", StringComparison.OrdinalIgnoreCase))
            {
                return "Missing Green LED";
            }

            if (message.Contains("ROI1", StringComparison.OrdinalIgnoreCase))
            {
                return message;
            }

            return "LED ROI not detected";
        }

        private static string FormatFrame(string frame)
        {
            return string.IsNullOrWhiteSpace(frame) ? "N/A" : frame;
        }

        private static (string ComPort, string Frame) ExtractFrame(string message, string marker)
        {
            var line = message
                .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault(x => x.TrimStart().StartsWith(marker, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(line))
            {
                return (string.Empty, string.Empty);
            }

            var trimmed = line.Trim();
            var portStart = trimmed.IndexOf('[', marker.Length);
            var portEnd = portStart >= 0 ? trimmed.IndexOf(']', portStart + 1) : -1;
            if (portStart < 0 || portEnd <= portStart)
            {
                return (string.Empty, trimmed);
            }

            var comPort = trimmed.Substring(portStart + 1, portEnd - portStart - 1);
            var frame = portEnd + 1 < trimmed.Length ? trimmed[(portEnd + 1)..].Trim() : string.Empty;
            return (comPort, frame);
        }

        private static string ExtractValue(string message)
        {
            const string marker = "Value=";
            var index = message.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return "N/A";
            }

            var value = message[(index + marker.Length)..].Trim();
            var lineBreak = value.IndexOfAny(new[] { '\r', '\n' });
            return lineBreak >= 0 ? value[..lineBreak].Trim() : value;
        }

        private static IReadOnlyList<string> ExtractDetectorRxTraceLines(string message)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return message
                .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => line.StartsWith("[TRACE] DT RX", StringComparison.OrdinalIgnoreCase))
                .Select(FormatDetectorRxTraceLine)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Where(line => seen.Add(line))
                .ToList();
        }

        private static string FormatDetectorRxTraceLine(string traceLine)
        {
            var attemptMarker = "attempt ";
            var attemptIndex = traceLine.IndexOf(attemptMarker, StringComparison.OrdinalIgnoreCase);
            var colonIndex = traceLine.IndexOf(':');
            if (attemptIndex < 0 || colonIndex <= attemptIndex)
            {
                return string.Empty;
            }

            var attempt = traceLine.Substring(attemptIndex + attemptMarker.Length, colonIndex - attemptIndex - attemptMarker.Length).Trim();
            var payload = colonIndex + 1 < traceLine.Length ? traceLine[(colonIndex + 1)..].Trim() : string.Empty;

            if (payload.StartsWith("timeout", StringComparison.OrdinalIgnoreCase))
            {
                return $"timeout (attempt {attempt})";
            }

            if (payload.StartsWith("invalid frame", StringComparison.OrdinalIgnoreCase))
            {
                return $"invalid frame (attempt {attempt})";
            }

            return $"{payload} (attempt {attempt})";
        }

        private static string NormalizeDeviceType(string deviceType)
        {
            return deviceType.ToLowerInvariant() switch
            {
                "smoke" => "smoke",
                "heat" => "heat",
                "bell" => "bell",
                "button" => "button",
                _ => string.IsNullOrWhiteSpace(deviceType) ? "unknown" : deviceType.Trim().ToLowerInvariant(),
            };
        }

        private static string GetDeviceLogName(string deviceType)
        {
            return deviceType.ToLowerInvariant() switch
            {
                "smoke" => "Đầu báo khói",
                "heat" => "Đầu báo nhiệt",
                "bell" => "Chuông đèn",
                "button" => "Nút bấm",
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

        private async void button1_Click(object sender, EventArgs e)
        {
            if (CurrentQrScanMode == QrScanMode.NoUse)
            {
                AppendLog("[INFO] QR No use: khong cho phep ket noi.");
                return;
            }
            if (comboBox1.SelectedItem is not string qrComPort || string.IsNullOrWhiteSpace(qrComPort))
            {
                AppendLog("Vui lòng chọn QR COM trước khi connect.");
                return;
            }

            try
            {
                button1.Enabled = false;

                if (_qrCodeScanService.IsConnected)
                {
                    await _qrCodeScanService.StopScanAsync().ConfigureAwait(true);
                    await _qrCodeScanService.DisconnectAsync().ConfigureAwait(true);
                    AppendLog($"Disconnect QR COM: {qrComPort}");
                }
                else
                {
                    await _qrCodeScanService.ConnectAsync(qrComPort).ConfigureAwait(true);
                    AppendLog($"Connect QR COM: {qrComPort} @ 9600");
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
            catch (HardwareException ex)
            {
                AppendLog($"[ERROR] {ex.Message}");
            }
            catch (UnauthorizedAccessException)
            {
                AppendLog($"[ERROR] COM {qrComPort} dang bi chiem. Hay dong ung dung khac dang su dung cong.");
            }
            finally
            {
                button1.Enabled = true;
                UpdateQrConnectButtonText();
            }
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

        private async Task<bool> ScanQrToSerialAsync(string qrComPort, CancellationToken ct)
        {
            if (!_qrCodeScanService.IsConnected ||
                !string.Equals(_qrCodeScanService.ConnectedComPort, qrComPort, StringComparison.OrdinalIgnoreCase))
            {
                await _qrCodeScanService.ConnectAsync(qrComPort, ct).ConfigureAwait(true);
                AppendLog($"Connect QR COM: {qrComPort} @ 9600");
            }

            AppendLog("[STEP] QR: gui trigger quet, timeout 5s");
            using var qrCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            qrCts.CancelAfter(TimeSpan.FromSeconds(5));

            try
            {
                var qrData = await _qrCodeScanService.ScanAsync(qrCts.Token).ConfigureAwait(true);
                txtSerial.Text = qrData.Value;
                AppendLog($"[QR][PASS] Serial={qrData.Value}");
                return true;
            }
            catch (TimeoutException ex)
            {
                AppendLog($"[QR][FAIL] Khong nhan duoc data QR trong 5s: {ex.Message}");
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                AppendLog("[QR][FAIL] Khong nhan duoc data QR trong 5s.");
            }
            finally
            {
                try
                {
                    await _qrCodeScanService.StopScanAsync(ct).ConfigureAwait(true);
                }
                catch (Exception ex) when (ex is InvalidOperationException or HardwareException)
                {
                    AppendLog($"[ERROR] QR stop scan that bai: {ex.Message}");
                }

                UpdateQrConnectButtonText();
            }

            return false;
        }

        private void radioButton8_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void groupBox2_Enter(object sender, EventArgs e)
        {

        }

        private void radioButton7_CheckedChanged(object sender, EventArgs e)
        {
            UpdateSerialInputState();
        }
    }
}
