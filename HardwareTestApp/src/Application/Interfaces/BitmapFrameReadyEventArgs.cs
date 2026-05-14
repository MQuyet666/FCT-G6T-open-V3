namespace FCT.G6T.Application.Interfaces;

public sealed class BitmapFrameReadyEventArgs : EventArgs
{
    public required Bitmap Frame { get; init; }
    public DateTime Timestamp { get; init; }
}
