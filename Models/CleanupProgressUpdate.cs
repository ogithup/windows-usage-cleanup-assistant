namespace WindowsUsageCleanupAssistant.Models;

public sealed class CleanupProgressUpdate
{
    public int ProcessedSteps { get; init; }

    public int TotalSteps { get; init; }

    public string CurrentStep { get; init; } = string.Empty;

    public string CurrentItem { get; init; } = string.Empty;

    public int DeletedEntries { get; init; }

    public int SkippedEntries { get; init; }

    public double PercentComplete => TotalSteps <= 0
        ? 0
        : Math.Round((double)ProcessedSteps / TotalSteps * 100, 1);
}
