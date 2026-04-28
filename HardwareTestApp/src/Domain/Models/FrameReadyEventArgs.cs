namespace FCT.G6T.Domain.Models;

public class FrameReadyEventArgs : EventArgs
{
    public required Bitmap Frame { get; init; }
    public DateTime Timestamp { get; init; }
}

