namespace FCT.G6T.Domain.Models;

public class G6TResponse
{
    public G6TCommandId CommandId { get; init; }
    public G6TStatus Status { get; init; }
    public string ComPort { get; init; } = string.Empty;
    public bool IsOpen { get; init; }
    public byte[] TxFrame { get; init; } = Array.Empty<byte>();
    public byte[] RxFrame { get; init; } = Array.Empty<byte>();
    public bool IsSuccess => Status == G6TStatus.Success;
}

public sealed class DetectorTraceEventArgs : EventArgs
{
    public DetectorTraceEventArgs(string message)
    {
        Message = message;
    }

    public string Message { get; }
}

public sealed class G6TTraceEventArgs : EventArgs
{
    public G6TTraceEventArgs(string message)
    {
        Message = message;
    }

    public string Message { get; }
}

