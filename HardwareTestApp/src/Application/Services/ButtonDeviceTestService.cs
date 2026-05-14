using FCT.G6T.Application.Interfaces;
using FCT.G6T.Domain.Exceptions;
using FCT.G6T.Domain.Interfaces;
using FCT.G6T.Domain.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FCT.G6T.Application.Services;

public class ButtonDeviceTestService : IButtonDeviceTestService
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
    private readonly IDetectorAdapter _detectorAdapter;
    private readonly ICameraPreviewAppService _cameraPreview;
    private readonly ILedDetectionService _ledDetectionService;
    private readonly ILogger<ButtonDeviceTestService> _logger;

    public ButtonDeviceTestService(
        TestOrchestrator testOrchestrator,
        IG6TAdapter g6tAdapter,
        IDetectorAdapter detectorAdapter,
        ICameraPreviewAppService cameraPreview,
        ILedDetectionService ledDetectionService,
        ILogger<ButtonDeviceTestService> logger,
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
        _ledDetectionService = ledDetectionService;
        _logger = logger;
        _g6tBaudRate = g6tBaudRate;
        _detectorBaudRate = detectorBaudRate;
        _ackTimeout = ackTimeout;
        _detectorAckTimeout = detectorAckTimeout;
        _ledDetectTimeout = ledDetectTimeout;
        _buttonTestTimeout = buttonTestTimeout;
        _ledDetectPollDelay = ledDetectPollDelay;
    }

    public bool IsConnected => _g6tAdapter.IsConnected;
    public bool IsDetectorConnected => _detectorAdapter.IsConnected;

    public Task ConnectAsync(string g6tComPort, CancellationToken ct = default)
    {
        return _g6tAdapter.ConnectAsync(g6tComPort, _g6tBaudRate, ct);
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        return _g6tAdapter.DisconnectAsync(ct);
    }

    public Task ConnectDetectorAsync(string detectorComPort, CancellationToken ct = default)
    {
        return _detectorAdapter.ConnectAsync(detectorComPort, _detectorBaudRate, ct);
    }

    public Task DisconnectDetectorAsync(CancellationToken ct = default)
    {
        return _detectorAdapter.DisconnectAsync(ct);
    }

    public async Task<IReadOnlyList<TestStepResult>> RunStartSequenceAsync(
        string g6tComPort,
        string detectorComPort,
        RoiRegion roi1,
        string deviceType,
        IReadOnlyList<RoiRegion>? buttonRois = null,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(g6tComPort))
        {
            throw new ArgumentException("Chưa chọn G6T COM.", nameof(g6tComPort));
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

        progress?.Report("[STEP] Cấp nguồn: gửi frame tới G6T, chờ ACK 3s");
        progress?.Report("[STEP] LED ROI Detect: chờ detect đủ màu đỏ + xanh + vàng");
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
        var ledDetectTask = _ledDetectionService.DetectButtonLedColorsAsync(roi1, buttonRois, _ledDetectTimeout, _ledDetectPollDelay, progress, ct);

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
            return results;
        }

        progress?.Report("[STEP] Button Test: gui activate_the_button toi G6T, delay 1s, roi detect ROI3 do");
        TestStepResult? buttonTestResult = null;
        const int maxAttempts = 2; // initial + 1 retry
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (attempt > 1)
            {
                progress?.Report($"[INFO] Button Test - retry attempt {attempt}");
            }

            var activateResult = await ExecuteCommandStepAsync(
                stepName: "Activate Button ACK",
                timeout: _ackTimeout,
                commandFunc: token => _testOrchestrator.ActivateTheButtonAsync(_ackTimeout, token),
                expectedCommandId: G6TCommandId.ActivateTheButton,
                progress: progress,
                ct: ct).ConfigureAwait(false);

            TestStepResult roi3Result;
            if (activateResult.IsPassed)
            {
                progress?.Report("[STEP] Button Test: delay 1s truoc khi detect ROI3");
                await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);

                roi3Result = await _ledDetectionService.DetectButtonRoi3RedAsync(
                    roi1,
                    buttonRois,
                    _ackTimeout,
                    _ledDetectPollDelay,
                    progress,
                    ct).ConfigureAwait(false);
            }
            else
            {
                roi3Result = new TestStepResult
                {
                    StepName = "Button ROI3 Red Detect",
                    IsPassed = false,
                    Message = "[ACK][FAIL] Button ROI3 Red Detect - skipped because activate ACK failed",
                };
            }

            var passed = activateResult.IsPassed && roi3Result.IsPassed;
            var buttonTestMessage =
                $"{activateResult.Message}{Environment.NewLine}" +
                $"{roi3Result.Message}{Environment.NewLine}" +
                $"[ACK][{(passed ? "PASS" : "FAIL")}] Button Test - Activate ACK={(activateResult.IsPassed ? "PASS" : "FAIL")}, ROI3 Red={(roi3Result.IsPassed ? "PASS" : "FAIL")}";

            buttonTestResult = new TestStepResult
            {
                StepName = "Button Test",
                IsPassed = passed,
                Message = buttonTestMessage,
            };
            progress?.Report(buttonTestMessage);

            results.Add(buttonTestResult);

            if (buttonTestResult.IsPassed)
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

        if (buttonTestResult is null || !buttonTestResult.IsPassed)
        {
            progress?.Report("[STEP] Button Test FAIL.");
            return results;
        }

        progress?.Report("[STEP] Calib Set ON: gui frame toi G6T, cho ACK 3s");
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
            return results;
        }

        progress?.Report("[STEP] UART On: gui frame toi G6T, cho ACK 3s");
        var uartOnCommand = new G6TCommand { CommandId = G6TCommandId.RelayOutput, Data = new byte[] { 0x05, 0x01 } };
        var uartOnResult = await ExecuteCommandStepWithRetryAsync(
            stepName: "UART On",
            timeout: _ackTimeout,
            commandFunc: token => _g6tAdapter.SendCommandAsync(uartOnCommand, token),
            expectedCommandId: G6TCommandId.RelayOutput,
            results: results,
            progress: progress,
            ct: ct).ConfigureAwait(false);
        if (!uartOnResult.IsPassed)
        {
            return results;
        }

        if (string.IsNullOrWhiteSpace(detectorComPort))
        {
            var message = "[ACK][FAIL] DT COM - Chua chon DT COM.";
            progress?.Report(message);
            results.Add(new TestStepResult
            {
                StepName = "DT COM",
                IsPassed = false,
                Message = message,
            });
            return results;
        }

        if (!_detectorAdapter.IsConnected)
        {
            try
            {
                await _detectorAdapter.ConnectAsync(detectorComPort, _detectorBaudRate, ct).ConfigureAwait(false);
                progress?.Report($"[STEP] Ket noi DT COM: {detectorComPort}");
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

        progress?.Report($"[STEP] Lora Test: gui frame toi DT COM, cho phan hoi {_detectorAckTimeout.TotalSeconds:0.#}s moi lan");
        var loraResult = await ExecuteDetectorReadStepAsync(
            stepName: "Lora Test",
            timeout: _detectorAckTimeout,
            readFunc: token => _detectorAdapter.ReadLoraAsync(token),
            validateFunc: response => string.Equals(response.Payload, "1.0.H(\u0001)", StringComparison.Ordinal),
            progress: progress,
            ct: ct).ConfigureAwait(false);
        results.Add(loraResult);

        return results;
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
            progress?.Report($"[WAITING] {stepName} - cho phan hoi {timeout.TotalSeconds:0.#}s");

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
        catch (OperationCanceledException)
        {
            var message = ct.IsCancellationRequested
                ? $"[ACK][FAIL] {stepName} - canceled"
                : $"[ACK][FAIL] {stepName} - timeout";
            progress?.Report(message);
            return new TestStepResult
            {
                StepName = stepName,
                IsPassed = false,
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
        var results = new List<TestStepResult>();
        await SendResetSequenceAsync(results, progress, ct).ConfigureAwait(false);
        return results;
    }

    public Task PrepareOnConnectAsync(string g6tComPort, IProgress<string>? progress = null, CancellationToken ct = default)
    {
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
            var portName = string.IsNullOrWhiteSpace(_g6tAdapter.ConnectedComPort) ? "UNKNOWN" : _g6tAdapter.ConnectedComPort;
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
        catch (HardwareException ex)
        {
            var message = $"[ACK][FAIL] {stepName} - hardware error: {ex.Message}";
            _logger.LogError(ex, "{Message}", message);
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
        if (string.IsNullOrEmpty(value))
        {
            return "<empty>";
        }

        var builder = new System.Text.StringBuilder();
        foreach (var ch in value)
        {
            builder.Append(ch == (char)1 ? "<SOH>" : ch);
        }

        return builder.ToString();
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

}
