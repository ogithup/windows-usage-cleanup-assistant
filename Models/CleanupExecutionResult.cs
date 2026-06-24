namespace WindowsUsageCleanupAssistant.Models;

public sealed class CleanupExecutionResult
{
    public int CategoryCount { get; init; }

    public int DeletedEntries { get; init; }

    public int SkippedEntries { get; init; }

    public double ReclaimedSizeMB { get; init; }

    public string Summary { get; init; } = string.Empty;
}
