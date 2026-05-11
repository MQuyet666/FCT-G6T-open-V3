using FCT.G6T.Application.Interfaces;
using FCT.G6T.Domain.Interfaces;
using FCT.G6T.Domain.Models;
using FCT.G6T.HAL.Serial;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FCT.G6T.Application.Services;

public class SmokeDeviceTestService : ISmokeDeviceTestService
{
    private readonly int _g6tBaudRate;
    private readonly int _detectorBaudRate;
    private readonly TimeSpan _ackTimeout;
    private readonly TimeSpan _detectorAckTimeout;
    private readonly TimeSpan _ledDetectTimeout;
    private readonly TimeSpan _buttonTestTimeout;
    private readonly TimeSpan _ledDetectPollDelay;

    private readonly TestOrchestrator _testOrchestrator;
    private readonly IG6TAdapter _g6tAdapter;
    protected readonly IDetectorAdapter _detectorAdapter;
    private readonly ICameraPreviewAppService _cameraPreview;
    private readonly ILogger<SmokeDeviceTestService> _logger;

    public SmokeDeviceTestService(
        TestOrchestrator testOrchestrator,
        IG6TAdapter g6tAdapter,
        IDetectorAdapter detectorAdapter,
        ICameraPreviewAppService cameraPreview,
        ILogger<SmokeDeviceTestService> logger,
        int g6tBaudRate,
        int detectorBaudRate,
        TimeSpan ackTimeout,
        TimeSpan detectorAckTimeout,
        TimeSpan ledDetectTimeout,
        TimeSpan buttonTestTimeout,
        TimeSpan ledDetectPollDelay)
    {
        _testOrchestrator = testOrchestrator;
        _g6tAdapter = g6tAdapter;
        _detectorAdapter = detectorAdapter;
        _cameraPreview = cameraPreview;
        _logger = logger;
        _g6tBaudRate = g6tBaudRate;
        _detectorBaudRate = detectorBaudRate;
        _ackTimeout = ackTimeout;
        _detectorAckTimeout = detectorAckTimeout;
        _ledDetectTimeout = ledDetectTimeout;
        _buttonTestTimeout = buttonTestTimeout;
        _ledDetectPollDelay = ledDetectPollDelay;
    }

    // Helper to avoid bringing Presentation types into Application layer
    private bool _currentDeviceTypeEquals(string expected)
    {
        // Application layer does not track UI device selection; assume consumer passes appropriate detector flow.
        // Return true for now if expected == "smoke" to satisfy requested behavior.
        return string.Equals(expected, "smoke", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsConnected => _g6tAdapter.IsConnected;
    public bool IsDetectorConnected => _detectorAdapter.IsConnected;

    public Task ConnectAsync(string g6tComPort, CancellationToken ct = default)
    {
        if (_detectorAdapter.IsConnected && string.Equals(_detectorAdapter.ConnectedComPort, g6tComPort, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"G6T COM trùng với DT COM ({g6tComPort}). Hãy chọn cổng khác.");
        }

        return _g6tAdapter.ConnectAsync(g6tComPort, _g6tBaudRate, ct);
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        return _g6tAdapter.DisconnectAsync(ct);
    }

    public Task ConnectDetectorAsync(string detectorComPort, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(detectorComPort))
        {
            throw new ArgumentException("Chưa chọn DT COM.", nameof(detectorComPort));
        }

        if (_g6tAdapter.IsConnected && string.Equals(_g6tAdapter.ConnectedComPort, detectorComPort, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"DT COM trùng với G6T COM ({detectorComPort}). Hãy chọn cổng khác.");
        }

        return _detectorAdapter.ConnectAsync(detectorComPort, _detectorBaudRate, ct);
    }

    public Task DisconnectDetectorAsync(CancellationToken ct = default)
    {
        return _detectorAdapter.DisconnectAsync(ct);
    }

    public async Task<IReadOnlyList<TestStepResult>> RunStartSequenceAsync(
        string g6tComPort,
        string detectorComPort,
        Rectangle roi1,
        string deviceType,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(g6tComPort))
        {
            throw new ArgumentException("Chưa chọn G6T COM.", nameof(g6tComPort));
        }

        if (string.IsNullOrWhiteSpace(detectorComPort))
        {
            throw new ArgumentException("Chưa chọn DT COM.", nameof(detectorComPort));
        }

        if (string.Equals(g6tComPort, detectorComPort, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"G6T COM trùng với DT COM ({g6tComPort}). Hãy chọn cổng khác.");
        }

        if (!_g6tAdapter.IsConnected)
        {
            await _g6tAdapter.ConnectAsync(g6tComPort, _g6tBaudRate, ct).ConfigureAwait(false);
            progress?.Report($"[STEP] Kết nối G6T COM: {g6tComPort}");
        }

        if (!_cameraPreview.IsRunning)
        {
            _cameraPreview.StartPreview();
            progress?.Report("[STEP] Bật camera preview để detect ROI1");
        }

        var results = new List<TestStepResult>();

        // Step 1: Power ON and LED detect run together. LED Test requires both G6T ACK and PC LED detection.
        progress?.Report("[STEP] Cấp nguồn: gửi frame tới G6T, chờ ACK 3s");
        progress?.Report("[STEP] LED ROI Detect: chờ detect đủ màu đỏ + xanh");
        progress?.Report("[ROI1][RESET]");

        var powerResults = new List<TestStepResult>();
        var powerTask = ExecuteCommandStepWithRetryAsync(
            stepName: "Cấp nguồn",
            timeout: _ackTimeout,
            commandFunc: token => _testOrchestrator.PowerOnAsync(token),
            expectedCommandId: G6TCommandId.PowerControl,
            results: powerResults,
            progress: null,
            ct: ct);
        var ledDetectTask = DetectLedColorsAsync(roi1, _ledDetectTimeout, ct, progress);

        await Task.WhenAll(powerTask, ledDetectTask).ConfigureAwait(false);

        var powerResult = await powerTask.ConfigureAwait(false);
        var ledDetectResult = await ledDetectTask.ConfigureAwait(false);

        var ledTestPassed = powerResult.IsPassed && ledDetectResult.IsPassed;
        var ledTestMessage = ledTestPassed
            ? "[ACK][PASS] LED Test - ACK G6T PASS, LED ROI PASS"
            : $"[ACK][FAIL] LED Test - ACK G6T={(powerResult.IsPassed ? "PASS" : "FAIL")}, LED ROI={(ledDetectResult.IsPassed ? "PASS" : "FAIL")}";

        progress?.Report(ledTestMessage);
        results.Add(new TestStepResult
        {
            StepName = "LED Test",
            IsPassed = ledTestPassed,
            Message = ledTestMessage,
        });
        results.AddRange(powerResults);
        results.Add(ledDetectResult);

        if (!ledTestPassed)
        {
            progress?.Report("[ACK][FAIL] LED Test - dừng flow vì chưa đạt điều kiện.");
            progress?.Report("[STEP] Bỏ qua DT COM: LED Test FAIL.");
            return results;
        }

        progress?.Report("[STEP] Button Test: gửi frame tới G6T, chờ ACK 15s");
        TestStepResult? buttonTestResult = null;
        const int maxAttempts = 2; // initial + 1 retry
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (attempt > 1)
            {
                progress?.Report($"[INFO] Button Test - retry attempt {attempt}");
            }

            buttonTestResult = await ExecuteCommandStepAsync(
                stepName: "Button Test",
                timeout: _buttonTestTimeout,
                commandFunc: token => _testOrchestrator.TestButtonBuzzerAsync(token),
                expectedCommandId: G6TCommandId.TestButton,
                progress: progress,
                ct: ct).ConfigureAwait(false);

            results.Add(buttonTestResult);

            if (buttonTestResult.IsPassed)
            {
                break;
            }

            // if not passed and we have remaining attempts, loop to retry
            if (attempt < maxAttempts)
            {
                try
                {
                    await Task.Delay(150, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        if (buttonTestResult is null || !buttonTestResult.IsPassed)
        {
            progress?.Report("[STEP] Bỏ qua DT COM: Button Test FAIL.");
            return results;
        }

        // After Button Test PASS, send UART_On frame to G6T (RelayOutput 0x08, data {0x05,0x01})
        progress?.Report("[STEP] UART_On: gửi frame tới G6T, chờ ACK 3s");
        var uartOnCmd = new G6TCommand { CommandId = G6TCommandId.RelayOutput, Data = new byte[] { 0x05, 0x01 } };
        var uartOnResult = await ExecuteCommandStepWithRetryAsync(
            stepName: "UART On",
            timeout: _ackTimeout,
            commandFunc: token => _g6tAdapter.SendCommandAsync(uartOnCmd, token),
            expectedCommandId: G6TCommandId.RelayOutput,
            results: results,
            progress: progress,
            ct: ct).ConfigureAwait(false);
        if (!uartOnResult.IsPassed)
        {
            // Log failure but continue with detector reads
            progress?.Report("[ACK][FAIL] UART On - không ảnh hưởng tới bước đọc giá trị.");
        }
        // After UART_On, set Calib Pin ON on G6T, then proceed to DT COM reads
        progress?.Report("[STEP] Calib Set ON: gửi frame tới G6T, chờ ACK 3s");
        var calibSetOnResult = await ExecuteCommandStepWithRetryAsync(
            stepName: "Calib Set ON",
            timeout: _ackTimeout,
            commandFunc: token => _testOrchestrator.SetCalibPinAsync(true, token),
            expectedCommandId: G6TCommandId.SetCalibPin,
            results: results,
            progress: progress,
            ct: ct).ConfigureAwait(false);

        if (!calibSetOnResult.IsPassed)
        {
            progress?.Report("[ACK][WARN] Calib Set ON thất bại - tiếp tục chu trình nhưng kiểm tra thiết bị.");
        }

        progress?.Report("[STEP] DT COM: bắt đầu đọc giá trị LoRa, nhiệt và khói.");
        if (!_detectorAdapter.IsConnected)
        {
            try
            {
                await _detectorAdapter.ConnectAsync(detectorComPort, _detectorBaudRate, ct).ConfigureAwait(false);
                progress?.Report($"[STEP] Kết nối DT COM: {detectorComPort}");
            }
            catch (Exception ex) when (ex is InvalidOperationException or HardwareException or UnauthorizedAccessException or IOException)
            {
                var message = $"[ACK][FAIL] DT COM - Khong mo duoc {detectorComPort}: {ex.Message}";
                progress?.Report(message);
                results.Add(new TestStepResult
                {
                    StepName = "DT COM",
                    IsPassed = false,
                    Message = message,
                });
                return results;
            }
        }
        else
        {
            progress?.Report($"[STEP] DT COM đã kết nối: {_detectorAdapter.ConnectedComPort}");
        }

        var loraLabel = string.Equals(deviceType, "smoke", StringComparison.OrdinalIgnoreCase)
            ? "đầu báo khói"
            : "đầu báo nhiệt";
        progress?.Report($"[STEP] Lora Test: gửi frame tới {loraLabel}, chờ ACK 3s");
        var loraResult = await ExecuteDetectorReadStepAsync(
            stepName: "Lora Test",
            timeout: _detectorAckTimeout,
            readFunc: token => _detectorAdapter.ReadLoraAsync(token),
            validateFunc: response => string.Equals(response.Payload, "1.0.H(\u0001)", StringComparison.Ordinal),
            progress: progress,
            ct: ct).ConfigureAwait(false);
        results.Add(loraResult);
        if (!loraResult.IsPassed)
        {
            return results;
        }

        var readValueResult = await ExecuteDetectorReadStepAsync(
            stepName: "Read Value Test",
            timeout: _detectorAckTimeout,
            readFunc: ReadDetectorValueAsync,
            validateFunc: ValidateDetectorValueResponse,
            progress: progress,
            ct: ct).ConfigureAwait(false);
        results.Add(readValueResult);
        return results;
    }

    protected virtual Task<DetectorResponse> ReadDetectorValueAsync(CancellationToken ct)
    {
        return _detectorAdapter.ReadSmokeAsync(ct);
    }

    protected virtual bool ValidateDetectorValueResponse(DetectorResponse response)
    {
        return response.Payload.StartsWith("1.0.5(", StringComparison.Ordinal) &&
            response.Payload.EndsWith(")", StringComparison.Ordinal);
    }

    private async Task<TestStepResult> ExecuteDetectorReadStepAsync(
        string stepName,
        TimeSpan timeout,
        Func<CancellationToken, Task<DetectorResponse>> readFunc,
        Func<DetectorResponse, bool>? validateFunc,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        try
        {
            progress?.Report($"[SENDING] {stepName} -> {_detectorAdapter.ConnectedComPort}");
            progress?.Report($"[WAITING] {stepName} - chờ ACK {timeout.TotalSeconds:0.#}s");

            var response = await readFunc(ct).ConfigureAwait(false);

            var passed = validateFunc?.Invoke(response) ?? true;

            var statusText = passed ? "PASS" : "FAIL";
            var message =
                $"[PORT][{response.ComPort}] IsOpen={_detectorAdapter.IsConnected}{Environment.NewLine}" +
                $"[TX][{response.ComPort}] {ToHex(response.TxFrame)}{Environment.NewLine}" +
                $"[RX][{response.ComPort}] {ToHex(response.RxFrame)}{Environment.NewLine}" +
                $"[ACK][{statusText}] {stepName} - Value={FormatDetectorValue(response.Value)}";
            if (response.TraceLines.Count > 0)
            {
                message += Environment.NewLine + string.Join(
                    Environment.NewLine,
                    response.TraceLines.Select(line => $"[TRACE] {line}"));
            }

            progress?.Report(message);
            return new TestStepResult
            {
                StepName = stepName,
                IsPassed = passed,
                Message = message,
            };
        }
        catch (Exception ex) when (ex is TimeoutException || ex is InvalidDataException || ex is InvalidOperationException)
        {
            var message = BuildInvalidFrameLog(stepName, ex.Message);
            progress?.Report(message);
            return new TestStepResult
            {
                StepName = stepName,
                IsPassed = false,
                Message = message,
            };
        }
    }

    public async Task<IReadOnlyList<TestStepResult>> SendResetAsync(string g6tComPort, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        // Ensure reset executed after UI showed final result (ACK wait, retry handled by adapter)
        // g6tComPort is accepted for compatibility with UI, adapter uses its connected port internally
        var results = new List<TestStepResult>();
        await SendResetSequenceAsync(results, progress, ct).ConfigureAwait(false);
        return results;
    }

    public Task PrepareOnConnectAsync(string g6tComPort, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        // Immediately put DUT into safe initial state after connect without blocking on ACK.
        return SendResetSequenceNoAckAsync(progress, ct);
    }

    private async Task SendResetSequenceNoAckAsync(IProgress<string>? progress, CancellationToken ct)
    {
        var powerOffCommand = new G6TCommand
        {
            CommandId = G6TCommandId.PowerControl,
            Data = new byte[] { (byte)G6TPowerState.Off },
        };

        progress?.Report("[STEP] RESET: gửi lệnh Cấp nguồn OFF (no-ack)");
        await _g6tAdapter.SendCommandNoAckAsync(powerOffCommand, ct).ConfigureAwait(false);

        var calibOffCommand = new G6TCommand
        {
            CommandId = G6TCommandId.SetCalibPin,
            Data = new[] { (byte)G6TCalibPinState.Unset },
        };

        progress?.Report("[STEP] RESET: gửi lệnh Set Calib Pin OFF (no-ack)");
        await _g6tAdapter.SendCommandNoAckAsync(calibOffCommand, ct).ConfigureAwait(false);

        var uartOffCommand = new G6TCommand
        {
            CommandId = G6TCommandId.RelayOutput,
            Data = new byte[] { 0x05, 0x00 },
        };

        progress?.Report("[STEP] RESET: gửi lệnh UART OFF (no-ack)");
        await _g6tAdapter.SendCommandNoAckAsync(uartOffCommand, ct).ConfigureAwait(false);
    }

    private async Task SendResetSequenceAsync(
        List<TestStepResult> results,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var restoreAckTimeout = TimeSpan.FromSeconds(2);

        // Send PowerOff first then SetCalibPin OFF as requested
        progress?.Report("[STEP] RESET: gửi lệnh Cấp nguồn OFF, chờ ACK 2s");
        await ExecuteCommandStepWithRetryAsync(
            stepName: "Reset PowerOff",
            timeout: restoreAckTimeout,
            commandFunc: token => _testOrchestrator.PowerOffAsync(token),
            expectedCommandId: G6TCommandId.PowerControl,
            results: results,
            progress: progress,
            ct: ct).ConfigureAwait(false);

        progress?.Report("[STEP] RESET: gửi lệnh Set Calib Pin OFF, chờ ACK 2s");
        await ExecuteCommandStepWithRetryAsync(
            stepName: "Reset SetCalibPinOff",
            timeout: restoreAckTimeout,
            commandFunc: token => _testOrchestrator.SetCalibPinAsync(false, token),
            expectedCommandId: G6TCommandId.SetCalibPin,
            results: results,
            progress: progress,
            ct: ct).ConfigureAwait(false);

        progress?.Report("[STEP] RESET: gửi lệnh UART OFF, chờ ACK 2s");
        var uartOffCommand = new G6TCommand
        {
            CommandId = G6TCommandId.RelayOutput,
            Data = new byte[] { 0x05, 0x00 },
        };
        await ExecuteCommandStepWithRetryAsync(
            stepName: "Reset UartOff",
            timeout: restoreAckTimeout,
            commandFunc: token => _g6tAdapter.SendCommandAsync(uartOffCommand, token),
            expectedCommandId: G6TCommandId.RelayOutput,
            results: results,
            progress: progress,
            ct: ct).ConfigureAwait(false);
    }

    private async Task<TestStepResult> ExecuteCommandStepAsync(
        string stepName,
        TimeSpan timeout,
        Func<CancellationToken, Task<G6TResponse>> commandFunc,
        G6TCommandId expectedCommandId,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        try
        {
            // Report real-time sending + waiting status to UI
            var portName = (_g6tAdapter as dynamic)?.ConnectedComPort as string ?? "UNKNOWN";
            progress?.Report($"[SENDING] {stepName} -> {portName}");
            progress?.Report($"[WAITING] {stepName} - chờ ACK {timeout.TotalSeconds:0.#}s");

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);

            var response = await commandFunc(timeoutCts.Token).ConfigureAwait(false);
            var passed = response.IsSuccess && response.CommandId == expectedCommandId;
            var ackStatus = passed ? "PASS" : "FAIL";
            var message =
                $"[PORT][{response.ComPort}] IsOpen={response.IsOpen}{Environment.NewLine}" +
                $"[TX][{response.ComPort}] {ToHex(response.TxFrame)}{Environment.NewLine}" +
                $"[RX][{response.ComPort}] {ToHex(response.RxFrame)}{Environment.NewLine}" +
                $"[ACK][{ackStatus}] {stepName} - Command={response.CommandId}, Status={response.Status}";

            _logger.LogInformation("{Message}", message);
            progress?.Report(message);
            return new TestStepResult
            {
                StepName = stepName,
                IsPassed = passed,
                Message = message,
            };
        }
        catch (OperationCanceledException)
        {
            var message = ct.IsCancellationRequested
                ? $"[ACK][FAIL] {stepName} - canceled"
                : $"[ACK][FAIL] {stepName} - timeout";
            _logger.LogWarning("{Message}", message);
            progress?.Report(message);
            return new TestStepResult
            {
                StepName = stepName,
                IsPassed = false,
                Message = message,
            };
        }
        catch (TimeoutException ex)
        {
            var message = BuildInvalidFrameLog(stepName, ex.Message).Replace("[ACK][FAIL]", "[ACK][FAIL]");
            _logger.LogWarning("{Message}", message);
            progress?.Report(message);
            return new TestStepResult
            {
                StepName = stepName,
                IsPassed = false,
                Message = message,
            };
        }
        catch (InvalidDataException ex)
        {
            var message = BuildInvalidFrameLog(stepName, ex.Message);
            _logger.LogWarning("{Message}", message);
            progress?.Report(message);
            return new TestStepResult
            {
                StepName = stepName,
                IsPassed = false,
                Message = message,
            };
        }

    }

    private async Task<TestStepResult> ExecuteCommandStepWithRetryAsync(
        string stepName,
        TimeSpan timeout,
        Func<CancellationToken, Task<G6TResponse>> commandFunc,
        G6TCommandId expectedCommandId,
        List<TestStepResult> results,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        TestStepResult? result = null;
        const int maxAttempts = 2; // initial + 1 retry

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (attempt > 1)
            {
                progress?.Report($"[INFO] {stepName} - retry attempt {attempt}");
            }

            result = await ExecuteCommandStepAsync(
                stepName: stepName,
                timeout: timeout,
                commandFunc: commandFunc,
                expectedCommandId: expectedCommandId,
                progress: progress,
                ct: ct).ConfigureAwait(false);

            results.Add(result);

            if (result.IsPassed)
            {
                break;
            }

            if (attempt < maxAttempts)
            {
                try
                {
                    await Task.Delay(150, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        return result ?? new TestStepResult
        {
            StepName = stepName,
            IsPassed = false,
            Message = $"[ACK][FAIL] {stepName} - canceled",
        };
    }

    private static string ToHex(byte[] data)
    {
        if (data is null || data.Length == 0)
        {
            return "<empty>";
        }

        return string.Join(" ", data.Select(x => x.ToString("X2")));
    }

    private static string FormatDetectorValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return "<empty>";

        var sb = new System.Text.StringBuilder();
        foreach (var ch in value)
        {
            if (ch == (char)1)
            {
                sb.Append("<SOH>");
            }
            else
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    private static string BuildInvalidFrameLog(string stepName, string rawError)
    {
        var parts = rawError.Split(" | ", StringSplitOptions.RemoveEmptyEntries);

        var tx = parts.FirstOrDefault(x => x.TrimStart().StartsWith("[TX]", StringComparison.OrdinalIgnoreCase));
        var rx = parts.FirstOrDefault(x => x.TrimStart().StartsWith("[RX]", StringComparison.OrdinalIgnoreCase));
        var portState = parts.FirstOrDefault(x => x.TrimStart().StartsWith("[PORT]", StringComparison.OrdinalIgnoreCase));

        var reasonParts = parts.Where(x =>
            !x.TrimStart().StartsWith("[TX]", StringComparison.OrdinalIgnoreCase) &&
            !x.TrimStart().StartsWith("[RX]", StringComparison.OrdinalIgnoreCase) &&
            !x.TrimStart().StartsWith("[PORT]", StringComparison.OrdinalIgnoreCase));

        var reason = string.Join(" | ", reasonParts);
        if (string.IsNullOrWhiteSpace(reason))
        {
            reason = "Frame không hợp lệ.";
        }

        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(tx))
        {
            lines.Add(tx.Trim());
        }

        if (!string.IsNullOrWhiteSpace(portState))
        {
            lines.Add(portState.Trim());
        }

        if (!string.IsNullOrWhiteSpace(rx))
        {
            lines.Add(rx.Trim());
        }

        lines.Add($"[ACK][FAIL] {stepName} - {reason.Trim()}");
        return string.Join(Environment.NewLine, lines);
    }

    private async Task<TestStepResult> DetectLedColorsAsync(
        Rectangle roi1,
        TimeSpan timeout,
        CancellationToken ct,
        IProgress<string>? progress)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        var redDetected = false;
        var greenDetected = false;

        while (!timeoutCts.IsCancellationRequested)
        {
            using var frame = await _cameraPreview.CaptureFrameAsync().ConfigureAwait(false);
            var boundedRoi = Rectangle.Intersect(new Rectangle(Point.Empty, frame.Size), roi1);
            if (boundedRoi.Width <= 0 || boundedRoi.Height <= 0)
            {
                return new TestStepResult
                {
                    StepName = "LED ROI Detect",
                    IsPassed = false,
                    Message = "ROI1 nằm ngoài khung ảnh.",
                };
            }

            AnalyzeColors(frame, boundedRoi, ref redDetected, ref greenDetected);
            if (redDetected && greenDetected)
            {
                var passMessage = "LED ROI Detect: phát hiện đủ màu đỏ và xanh.";
                _logger.LogInformation("{Message}", passMessage);
                progress?.Report("[ROI1][PASS]");
                progress?.Report($"[ACK][PASS] LED ROI Detect - {passMessage}");
                return new TestStepResult
                {
                    StepName = "LED ROI Detect",
                    IsPassed = true,
                    Message = $"[ACK][PASS] LED ROI Detect - {passMessage}",
                };
            }

            try
            {
                await Task.Delay(_ledDetectPollDelay, timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        var missingColors = (redDetected, greenDetected) switch
        {
            (false, false) => "Missing=Red,Green",
            (false, true) => "Missing=Red",
            (true, false) => "Missing=Green",
            _ => "Missing=None",
        };
        var failMessage = $"[ACK][FAIL] LED ROI Detect - không thấy đủ đỏ + xanh trong {timeout.TotalSeconds:0.#}s. {missingColors}";
        progress?.Report(failMessage);
        return new TestStepResult
        {
            StepName = "LED ROI Detect",
            IsPassed = false,
            Message = failMessage,
        };
    }

    private static void AnalyzeColors(Bitmap frame, Rectangle roi, ref bool redDetected, ref bool greenDetected)
    {
        const int sampleStep = 4;

        for (var y = roi.Top; y < roi.Bottom; y += sampleStep)
        {
            for (var x = roi.Left; x < roi.Right; x += sampleStep)
            {
                var color = frame.GetPixel(x, y);

                if (!redDetected && color.R > 180 && color.G < 120 && color.B < 120)
                {
                    redDetected = true;
                }

                if (!greenDetected && color.G > 150 && color.R < 150 && color.B < 150)
                {
                    greenDetected = true;
                }

                if (redDetected && greenDetected)
                {
                    return;
                }
            }
        }
    }
}

