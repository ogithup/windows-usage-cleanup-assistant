namespace WindowsUsageCleanupAssistant.Services;

public interface IProgramUsageTracker
{
    event EventHandler? UsageUpdated;

    void Start();

    Task StopAsync();
}
