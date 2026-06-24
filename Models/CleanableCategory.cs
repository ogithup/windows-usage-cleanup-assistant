namespace WindowsUsageCleanupAssistant.Models;

public sealed class CleanableCategory
{
    public string CategoryKey { get; init; } = string.Empty;

    public string CategoryName { get; init; } = string.Empty;

    public int FileCount { get; init; }

    public double TotalSizeMB { get; init; }

    public string RiskLevel { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool CanClean { get; init; }

    public bool MoveToRecycleBinWhenPossible { get; init; }
}
