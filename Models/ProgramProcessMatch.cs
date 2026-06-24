namespace WindowsUsageCleanupAssistant.Models;

public sealed class ProgramProcessMatch
{
    public string MatchedProcessName { get; init; } = string.Empty;

    public string MatchedExecutablePath { get; init; } = string.Empty;

    public int ConfidenceScore { get; init; }

    public DateTime? FirstSeenUtc { get; init; }

    public DateTime? LastSeenUtc { get; init; }

    public double TotalObservedMinutes { get; init; }

    public int LaunchCount { get; init; }
}
