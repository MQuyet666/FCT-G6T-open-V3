using HardwareTestApp.src.Application.Interfaces;
using HardwareTestApp.src.Domain.Models;

namespace HardwareTestApp.src.Application.Services;

public class TestCaseProvider : ITestCaseProvider
{
    public IReadOnlyList<TestCase> GetTestCasesForDevice(string deviceType)
    {
        return deviceType.ToLower() switch
        {
            "smock" => new List<TestCase>
            {
                new TestCase { Id = "TC_LED", Name = "LED Test" },
                new TestCase { Id = "TC_BTN", Name = "Button Test" },
                new TestCase { Id = "TC_LORA", Name = "Lora Test" },
                new TestCase { Id = "TC_READ", Name = "Read Value Test" },
            },
            "heate" => new List<TestCase>
            {
                new TestCase { Id = "TC_LED", Name = "LED Test" },
                new TestCase { Id = "TC_BTN", Name = "Button Test" },
            },
            "bale" => new List<TestCase>
            {
                new TestCase { Id = "TC_LORA", Name = "Lora Test" },
            },
            "button" => new List<TestCase>
            {
                new TestCase { Id = "TC_BTN", Name = "Button Test" },
            },
            _ => new List<TestCase>()
        };
    }
}
