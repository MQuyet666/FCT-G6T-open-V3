namespace FCT.G6T.Domain.Models;

public class TestStep
{
    public int Order { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string ExpectedLed { get; set; } = string.Empty;
    public int Timeout { get; set; }
    public int MaxRetry { get; set; }
}
