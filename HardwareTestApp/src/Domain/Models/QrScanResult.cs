namespace FCT.G6T.Domain.Models;

public sealed class QrScanResult
{
    public bool IsSkipped { get; init; }
    public bool IsScanned { get; init; }
    public QrCodeData? Data { get; init; }
    public string Message { get; init; } = string.Empty;

    public static QrScanResult Skipped(string message)
    {
        return new QrScanResult
        {
            IsSkipped = true,
            Message = message,
        };
    }

    public static QrScanResult Scanned(QrCodeData data)
    {
        return new QrScanResult
        {
            IsScanned = true,
            Data = data,
            Message = data.Value,
        };
    }

    public static QrScanResult Failed(string message)
    {
        return new QrScanResult
        {
            Message = message,
        };
    }
}
