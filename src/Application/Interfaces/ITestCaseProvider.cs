using FCT.G6T.Domain.Models;

namespace FCT.G6T.Application.Interfaces;

public interface ITestCaseProvider
{
    IReadOnlyList<TestCase> GetTestCasesForDevice(string deviceType);
}

