using System.IO;

namespace WindowsUsageCleanupAssistant.Services;

public sealed class FileCleanupLogService : ICleanupLogService
{
    private readonly string _logFilePath;
    private readonly object _syncRoot = new();

    public FileCleanupLogService(string logFilePath)
    {
        var directoryPath = Path.GetDirectoryName(logFilePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        _logFilePath = logFilePath;
    }

    public void Log(string message)
    {
        var entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}";

        lock (_syncRoot)
        {
            File.AppendAllText(_logFilePath, entry);
        }
    }

    public string GetLogFilePath()
    {
        return _logFilePath;
    }
}
