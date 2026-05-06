namespace FCT.G6T.Domain.Models;

public class DetectorResponse
{
    public string ComPort { get; init; } = string.Empty;
    public byte[] TxFrame { get; init; } = Array.Empty<byte>();
    public byte[] RxFrame { get; init; } = Array.Empty<byte>();
    public string Payload { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

