namespace HardwareTestApp.src.Domain.Models;

public class FrameReadyEventArgs : EventArgs
{
    public Bitmap Frame { get; init; }
    public DateTime Timestamp { get; init; }
}
