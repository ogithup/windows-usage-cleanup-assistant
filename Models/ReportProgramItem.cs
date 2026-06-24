namespace WindowsUsageCleanupAssistant.Models;

public sealed class ReportProgramItem
{
    public string DisplayName { get; init; } = string.Empty;

    public string Publisher { get; init; } = string.Empty;

    public string DisplayVersion { get; init; } = string.Empty;

    public long? EstimatedSizeMB { get; init; }

    public DateTime? InstallDate { get; init; }

    public DateTime? LastUsedUtc { get; init; }

    public string MatchedProcessName { get; init; } = string.Empty;

    public int MatchConfidence { get; init; }

    public int UsageScore { get; init; }

    public int SizeScore { get; init; }

    public int SystemRiskScore { get; init; }

    public int DependencyRiskScore { get; init; }

    public int FinalCleanupScore { get; init; }

    public string Recommendation { get; init; } = string.Empty;

    public string RecommendationReasons { get; init; } = string.Empty;
}
