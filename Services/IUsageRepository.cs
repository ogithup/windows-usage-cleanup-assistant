using WindowsUsageCleanupAssistant.Models;

namespace WindowsUsageCleanupAssistant.Services;

public interface IUsageRepository
{
    void Initialize();

    void RecordObservation(string processName, string executablePath, DateTime observedAtUtc, double observedMinutes, bool incrementLaunchCount);

    IReadOnlyList<UsageRecord> GetUsageRecords();
}
