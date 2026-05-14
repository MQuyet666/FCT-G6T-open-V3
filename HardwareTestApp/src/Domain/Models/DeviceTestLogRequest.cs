namespace FCT.G6T.Domain.Models;

public sealed class DeviceTestLogRequest
{
    public string DeviceType { get; init; } = string.Empty;
    public string Serial { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public IReadOnlyList<TestStepResult> FinalStepResults { get; init; } = Array.Empty<TestStepResult>();
    public IReadOnlyList<TestStepResult> RestoreStepResults { get; init; } = Array.Empty<TestStepResult>();
    public bool AllPassed { get; init; }
}
