namespace WindowsUsageCleanupAssistant.Services;

public interface ICleanupLogService
{
    void Log(string message);

    string GetLogFilePath();
}
