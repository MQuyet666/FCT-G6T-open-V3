namespace FCT.G6T.Domain.Models;

public class TestStepResult
{
    public string StepName { get; init; } = string.Empty;
    public bool IsPassed { get; init; }
    public string Message { get; init; } = string.Empty;
}

