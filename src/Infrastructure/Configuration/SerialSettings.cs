namespace FCT.G6T.Infrastructure.Configuration;

public class SerialSettings
{
    public int G6tBaudRate { get; set; } = 9600;
    public int DetectorBaudRate { get; set; } = 9600;
    public int QrBaudRate { get; set; } = 9600;
    public int ReadTimeoutMs { get; set; } = 100;
    public int WriteTimeoutMs { get; set; } = 1000;
    public int FrameRetryCount { get; set; } = 1;
    public int DetectorRetryCount { get; set; } = 1;
    public int FrameAckTimeoutSeconds { get; set; } = 3;
    public int DetectorAckTimeoutSeconds { get; set; } = 3;
    public int QrReadTimeoutSeconds { get; set; } = 5;
}
