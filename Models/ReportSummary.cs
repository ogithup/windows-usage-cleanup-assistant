namespace WindowsUsageCleanupAssistant.Models;

public sealed class ReportSummary
{
    public int TotalInstalledPrograms { get; init; }

    public int UnusedSixMonthsCount { get; init; }

    public int CleanupCandidateCount { get; init; }

    public int DependencySensitiveCount { get; init; }

    public double TemporaryCleanupSizeMB { get; init; }

    public double EstimatedReclaimableSizeMB { get; init; }
}
