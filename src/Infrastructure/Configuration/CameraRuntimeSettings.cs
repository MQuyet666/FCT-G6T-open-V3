namespace FCT.G6T.Infrastructure.Configuration;

public class CameraRuntimeSettings
{
    public int OpenCvFrameDelayMs { get; set; } = 33;
    public int FrameBufferSize { get; set; } = 3;
    public int CameraRetryIntervalSeconds { get; set; } = 2;
}
