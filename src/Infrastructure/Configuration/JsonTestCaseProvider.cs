using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FCT.G6T.Application.Interfaces;
using FCT.G6T.Domain.Models;

namespace FCT.G6T.Infrastructure.Configuration;

public class JsonTestCaseProvider : ITestCaseProvider
{
    private readonly string _configDirectory;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public JsonTestCaseProvider(string configDirectory)
    {
        _configDirectory = configDirectory;
    }

    public IReadOnlyList<TestCase> GetTestCasesForDevice(string deviceType)
    {
        if (string.IsNullOrWhiteSpace(deviceType))
        {
            return Array.Empty<TestCase>();
        }

        var fileName = $"{deviceType.ToLowerInvariant()}-test-cases.json";
        var path = Path.Combine(_configDirectory, fileName);
        if (!File.Exists(path))
        {
            return Array.Empty<TestCase>();
        }

        var json = File.ReadAllText(path);
        List<TestCase>? testCases = JsonSerializer.Deserialize<List<TestCase>>(json, _serializerOptions);
        return testCases is null ? Array.Empty<TestCase>() : testCases;
    }
}
