namespace WindowsUsageCleanupAssistant.Models;

public sealed class CleanupRecommendation
{
    public int UsageScore { get; init; }

    public int SizeScore { get; init; }

    public int SystemRiskScore { get; init; }

    public int DependencyRiskScore { get; init; }

    public int FinalCleanupScore { get; init; }

    public string Recommendation { get; init; } = "Unknown";

    public string Reasons { get; init; } = string.Empty;
}
