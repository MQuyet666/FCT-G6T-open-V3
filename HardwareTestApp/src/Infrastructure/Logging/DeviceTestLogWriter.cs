using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FCT.G6T.Application.Interfaces;
using FCT.G6T.Domain.Models;

namespace FCT.G6T.Infrastructure.Logging;

public sealed class DeviceTestLogWriter : IDeviceTestLogWriter
{
    private const string AddLogConfigFileName = "Address_log.json";

    public Task<string> WriteAsync(DeviceTestLogRequest request, CancellationToken ct = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        ct.ThrowIfCancellationRequested();

        var resultSuffix = request.AllPassed ? "pass" : "fail";
        var resultPath = ResolveLogPath(request.DeviceType, resultSuffix, request.Timestamp);
        Directory.CreateDirectory(Path.GetDirectoryName(resultPath) ?? AppContext.BaseDirectory);

        var builder = new StringBuilder();
        builder.AppendLine("//***********************************START TEST**********************************//");
        builder.AppendLine();
        builder.AppendLine($"DEVICE NAME : {GetDeviceLogName(request.DeviceType)}");
        builder.AppendLine($"SERIAL      : {FormatSerial(request.Serial)}");
        builder.AppendLine($"DATE TIME   : {request.Timestamp:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine();

        if (TryAppendQrFailLog(builder, request.FinalStepResults, request.AllPassed))
        {
            File.AppendAllText(resultPath, builder.ToString(), Encoding.UTF8);
            return Task.FromResult(resultPath);
        }

        AppendPowerOnLog(builder, request.FinalStepResults, request.AllPassed);
        AppendCommandStepLog(builder, request.FinalStepResults, "Button Test", request.AllPassed ? "BUTTON" : null);
        AppendCommandStepLog(builder, request.FinalStepResults, "UART On", null, appendBlankLine: false);
        AppendCommandStepLog(builder, request.FinalStepResults, "Calib Set ON", null);
        AppendDetectorStepLog(builder, request.FinalStepResults, "Lora Test", includeValue: false, request.AllPassed ? "LORA" : null);
        AppendDetectorStepLog(builder, request.FinalStepResults, "Read Value Test", includeValue: true, request.AllPassed ? "SENSOR" : null);

        builder.AppendLine();
        if (!request.AllPassed)
        {
            builder.AppendLine($"[RESULT] : FAIL {BuildFailSummary(request.DeviceType, request.FinalStepResults)}");
            builder.AppendLine();
            builder.AppendLine();
        }
        builder.AppendLine("//***********************************END TEST***********************************//");

        File.AppendAllText(resultPath, builder.ToString(), Encoding.UTF8);
        return Task.FromResult(resultPath);
    }

    private static string ResolveLogPath(string deviceType, string resultSuffix, DateTime timestamp)
    {
        var normalizedDeviceType = NormalizeDeviceType(deviceType);
        var status = resultSuffix.Equals("pass", StringComparison.OrdinalIgnoreCase) ? "PASS" : "FAIL";
        var fallbackFileName = $"device-{normalizedDeviceType}-{resultSuffix}.txt";
        var fallbackPath = Path.Combine(AppContext.BaseDirectory, "logs", fallbackFileName);

        var configPath = Path.Combine(AppContext.BaseDirectory, AddLogConfigFileName);
        if (!File.Exists(configPath))
        {
            return fallbackPath;
        }

        try
        {
            var json = File.ReadAllText(configPath, Encoding.UTF8);
            var entries = JsonSerializer.Deserialize<List<DeviceLogPathConfig>>(json) ?? new List<DeviceLogPathConfig>();
            var match = entries.FirstOrDefault(entry =>
                entry.DeviceName.Equals(normalizedDeviceType, StringComparison.OrdinalIgnoreCase) &&
                entry.Status.Equals(status, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                return fallbackPath;
            }

            var configuredPath = !string.IsNullOrWhiteSpace(match.FilePath)
                ? match.FilePath.Trim()
                : Path.Combine("logs", string.IsNullOrWhiteSpace(match.FileName) ? fallbackFileName : match.FileName.Trim());
            configuredPath = ApplyTimestampTokens(configuredPath, timestamp);

            return Path.GetFullPath(Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(AppContext.BaseDirectory, configuredPath));
        }
        catch
        {
            return fallbackPath;
        }
    }

    private static string ApplyTimestampTokens(string value, DateTime timestamp)
    {
        return value
            .Replace("{yyyyMMddHHmm}", timestamp.ToString("yyyyMMddHHmm"), StringComparison.OrdinalIgnoreCase)
            .Replace("{yyyyMMddHH}", timestamp.ToString("yyyyMMddHH"), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class DeviceLogPathConfig
    {
        [JsonPropertyName("device_name")]
        public string DeviceName { get; init; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; init; } = string.Empty;

        [JsonPropertyName("file_name")]
        public string FileName { get; init; } = string.Empty;

        [JsonPropertyName("file_path")]
        public string FilePath { get; init; } = string.Empty;
    }

    private static string FormatSerial(string serial)
    {
        return string.IsNullOrWhiteSpace(serial) ? "N/A" : serial.Trim();
    }

    private static string BuildFailSummary(string deviceType, IReadOnlyList<TestStepResult> results)
    {
        var names = results
            .Where(step => !step.IsPassed && !step.StepName.Equals("QR Scan", StringComparison.OrdinalIgnoreCase))
            .Select(step => MapFailLabel(deviceType, step.StepName))
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return names.Count == 0 ? "UNKNOWN" : string.Join(", ", names);
    }

    private static string MapFailLabel(string deviceType, string stepName)
    {
        if (stepName.Equals("LED ROI Detect", StringComparison.OrdinalIgnoreCase) ||
            stepName.Equals("LED Test", StringComparison.OrdinalIgnoreCase))
        {
            return "LED";
        }

        if (stepName.Equals("Button Test", StringComparison.OrdinalIgnoreCase))
        {
            if (NormalizeDeviceType(deviceType).Equals("button", StringComparison.OrdinalIgnoreCase))
            {
                return "LED";
            }

            return "BUZZER";
        }

        if (stepName.Equals("Lora Test", StringComparison.OrdinalIgnoreCase))
        {
            return "LORA";
        }

        if (stepName.Equals("Read Value Test", StringComparison.OrdinalIgnoreCase))
        {
            return "READ VALUE";
        }

        return stepName;
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
        builder.AppendLine("[RESULT ]    : FAIL");
        builder.AppendLine();
        builder.AppendLine("//***********************************END TEST***********************************//");
        return true;
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

    private static void AppendPowerOnLog(StringBuilder builder, IReadOnlyList<TestStepResult> results, bool includeLabels)
    {
        var powerResult = results.LastOrDefault(step =>
            !step.StepName.StartsWith("Reset", StringComparison.OrdinalIgnoreCase) &&
            step.Message.Contains("Command=PowerControl", StringComparison.OrdinalIgnoreCase));
        var ledResult = results.LastOrDefault(step =>
            step.StepName.Equals("LED ROI Detect", StringComparison.OrdinalIgnoreCase));
        var tx = ExtractFrame(powerResult?.Message ?? string.Empty, "[TX]");
        var rx = ExtractFrame(powerResult?.Message ?? string.Empty, "[RX]");

        builder.AppendLine($"TX : {FormatFrame(tx.Frame)}");
        builder.AppendLine($"RX : {FormatFrame(rx.Frame)}");
        if (includeLabels)
        {
            builder.AppendLine($"LED: {FormatLabelStatus(ledResult)}");
        }
        else
        {
            builder.AppendLine($"- LED ROI Detect : {FormatLedRoiStatus(ledResult)}");
        }
        builder.AppendLine();
    }

    private static void AppendCommandStepLog(
        StringBuilder builder,
        IReadOnlyList<TestStepResult> results,
        string stepName,
        string? label,
        bool appendBlankLine = true)
    {
        var result = results.LastOrDefault(step => step.StepName.Equals(stepName, StringComparison.OrdinalIgnoreCase));
        if (result is null)
        {
            return;
        }

        var tx = ExtractFrame(result.Message, "[TX]");
        var rx = ExtractFrame(result.Message, "[RX]");

        builder.AppendLine($"TX : {FormatFrame(tx.Frame)}");
        builder.AppendLine($"RX : {FormatFrame(rx.Frame)}");
        if (!string.IsNullOrWhiteSpace(label))
        {
            builder.AppendLine($"{label}: {FormatLabelStatus(result)}");
        }
        if (appendBlankLine)
        {
            builder.AppendLine();
        }
    }

    private static void AppendDetectorStepLog(
        StringBuilder builder,
        IReadOnlyList<TestStepResult> results,
        string stepName,
        bool includeValue,
        string? label)
    {
        var result = results.LastOrDefault(step => step.StepName.Equals(stepName, StringComparison.OrdinalIgnoreCase));
        if (result is null)
        {
            return;
        }

        var tx = ExtractFrame(result.Message, "[TX]");
        var rx = ExtractFrame(result.Message, "[RX]");

        builder.AppendLine($"TX : {FormatFrame(tx.Frame)}");
        builder.AppendLine($"RX : {FormatFrame(rx.Frame)}");

        if (includeValue)
        {
            builder.AppendLine($"VALUE     : {ExtractValue(result.Message)}");
        }
        if (!string.IsNullOrWhiteSpace(label))
        {
            builder.AppendLine($"{label}: {FormatLabelStatus(result)}");
        }
        builder.AppendLine();
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


    private static string FormatLedRoiStatus(TestStepResult? result)
    {
        if (result is null)
        {
            return "N/A";
        }

        return result.IsPassed ? "PASS (Detected Red + Green LED)" : $"FAIL ({GetLedRoiFailReason(result.Message)})";
    }

    private static string FormatLabelStatus(TestStepResult? result)
    {
        return result is not null && result.IsPassed ? "PASS" : "FAIL";
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
}
