using WindowsUsageCleanupAssistant.Models;

namespace WindowsUsageCleanupAssistant.ViewModels;

public sealed class InstalledProgramViewModel
{
    public required InstalledProgram Program { get; init; }

    public string DisplayName => Program.DisplayName;

    public string Publisher => Program.Publisher;

    public string DisplayVersion => Program.DisplayVersion;

    public string InstallLocation => Program.InstallLocation;

    public long? EstimatedSizeMB => Program.EstimatedSizeMB;

    public DateTime? InstallDate => Program.InstallDate;

    public string UninstallString => Program.UninstallString;

    public string MatchedProcessName { get; init; } = string.Empty;

    public int MatchConfidence { get; init; }

    public DateTime? LastUsedUtc { get; init; }

    public double TotalObservedMinutes { get; init; }

    public int LaunchCount { get; init; }

    public int UsageScore { get; init; }

    public int SizeScore { get; init; }

    public int SystemRiskScore { get; init; }

    public int DependencyRiskScore { get; init; }

    public int FinalCleanupScore { get; init; }

    public string Recommendation { get; init; } = "Unknown";

    public string RecommendationReasons { get; init; } = string.Empty;
}
