namespace WindowsUsageCleanupAssistant.Models;

public sealed class UsageRecord
{
    public string ProcessName { get; init; } = string.Empty;

    public string ExecutablePath { get; init; } = string.Empty;

    public DateTime FirstSeenUtc { get; init; }

    public DateTime LastSeenUtc { get; init; }

    public double TotalObservedMinutes { get; init; }

    public int LaunchCount { get; init; }
}
