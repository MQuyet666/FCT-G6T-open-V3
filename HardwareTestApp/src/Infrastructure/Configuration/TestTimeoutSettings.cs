namespace FCT.G6T.Infrastructure.Configuration;

public class TestTimeoutSettings
{
    public int CommandAckTimeoutSeconds { get; set; } = 3;
    public int LedDetectTimeoutSeconds { get; set; } = 5;
    public int ButtonTestTimeoutSeconds { get; set; } = 15;
    public int LedDetectPollDelayMs { get; set; } = 100;
}
