namespace FCT.G6T.Domain.Models;

public sealed class CameraFrame
{
    public required byte[] Bgr24Data { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int Stride { get; init; }
    public DateTime Timestamp { get; init; }
}
