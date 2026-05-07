namespace FCT.G6T.Domain.Models;

public sealed class QrCodeData
{
    public string ComPort { get; init; } = string.Empty;
    public int BaudRate { get; init; }
    public string Value { get; init; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.Now;
}
