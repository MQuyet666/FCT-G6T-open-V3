namespace FCT.G6T.Domain.Models;

public class TestCase
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public List<TestStep> Steps { get; set; } = new();
    public List<TestStep> Teardown { get; set; } = new();
}

