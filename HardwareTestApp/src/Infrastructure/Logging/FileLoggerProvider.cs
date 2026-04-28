using Microsoft.Extensions.Logging;

namespace FCT.G6T.Infrastructure.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logDirectory;
    private readonly string _filePrefix;
    private readonly int _retentionDays;
    private readonly object _sync = new();
    private DateTime _currentDate = DateTime.MinValue;
    private StreamWriter? _writer;

    public FileLoggerProvider(string logDirectory, string filePrefix, int retentionDays)
    {
        _logDirectory = logDirectory;
        _filePrefix = filePrefix;
        _retentionDays = retentionDays;
        Directory.CreateDirectory(_logDirectory);
        CleanupOldLogs();
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(this, categoryName);
    }

    internal void WriteLog(LogLevel level, string category, string message, Exception? exception)
    {
        var now = DateTime.Now;
        var line = $"{now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {category} - {message}";
        if (exception is not null)
        {
            line += $" | {exception.GetType().Name}: {exception.Message}";
        }

        lock (_sync)
        {
            EnsureWriter(now.Date);
            _writer?.WriteLine(line);
        }
    }

    private void EnsureWriter(DateTime date)
    {
        if (_currentDate == date && _writer is not null)
        {
            return;
        }

        _writer?.Dispose();
        _currentDate = date;
        var filePath = Path.Combine(_logDirectory, $"{_filePrefix}{_currentDate:yyyy-MM-dd}.log");
        _writer = new StreamWriter(new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true
        };
    }

    private void CleanupOldLogs()
    {
        if (_retentionDays <= 0)
        {
            return;
        }

        var threshold = DateTime.Today.AddDays(-_retentionDays);
        foreach (var file in Directory.EnumerateFiles(_logDirectory, $"{_filePrefix}*.log"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (!name.StartsWith(_filePrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var datePart = name.Substring(_filePrefix.Length);
            if (DateTime.TryParseExact(datePart, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var date) && date < threshold)
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // ignore cleanup errors
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
