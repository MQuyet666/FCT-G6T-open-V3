using HardwareTestApp.src.Domain.Models;

namespace HardwareTestApp.src.Application.Interfaces;

public interface ITestCaseProvider
{
    IReadOnlyList<TestCase> GetTestCasesForDevice(string deviceType);
}
