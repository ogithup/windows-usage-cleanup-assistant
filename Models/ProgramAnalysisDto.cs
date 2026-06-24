namespace WindowsUsageCleanupAssistant.Models;

public sealed class ProgramAnalysisDto
{
    public string ProgramName { get; init; } = string.Empty;

    public string Publisher { get; init; } = string.Empty;

    public long? SizeMB { get; init; }

    public DateTime? LastUsedUtc { get; init; }

    public string LastUsedLabel { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public IReadOnlyList<string> RiskFlags { get; init; } = [];

    public string Recommendation { get; init; } = string.Empty;

    public int FinalCleanupScore { get; init; }

    public string RecommendationReasons { get; init; } = string.Empty;
}
