using WindowsUsageCleanupAssistant.Models;

namespace WindowsUsageCleanupAssistant.Services;

public sealed class CleanupRecommendationEngine : ICleanupRecommendationEngine
{
    private static readonly string[] RiskyPublishers =
    [
        "Microsoft",
        "Intel",
        "NVIDIA",
        "AMD",
        "Realtek",
        "Oracle",
        "Eclipse Foundation",
        "JetBrains",
    ];

    private static readonly string[] DependencySensitiveKeywords =
    [
        "SDK",
        "Runtime",
        "Redistributable",
        "Driver",
        "Framework",
        "JDK",
        ".NET",
    ];

    public CleanupRecommendation Evaluate(InstalledProgram program, ProgramProcessMatch match)
    {
        var reasons = new List<string>();
        var usageScore = CalculateUsageScore(program, match, reasons);
        var sizeScore = CalculateSizeScore(program, reasons);
        var systemRiskScore = CalculateSystemRiskScore(program, reasons);
        var dependencyRiskScore = CalculateDependencyRiskScore(program, reasons);

        var finalCleanupScore = Math.Clamp(usageScore + sizeScore - systemRiskScore - dependencyRiskScore, 0, 100);
        var recommendation = DetermineRecommendation(program, match, finalCleanupScore, systemRiskScore, dependencyRiskScore, reasons);

        return new CleanupRecommendation
        {
            UsageScore = usageScore,
            SizeScore = sizeScore,
            SystemRiskScore = systemRiskScore,
            DependencyRiskScore = dependencyRiskScore,
            FinalCleanupScore = finalCleanupScore,
            Recommendation = recommendation,
            Reasons = string.Join(" | ", reasons.Distinct(StringComparer.CurrentCultureIgnoreCase)),
        };
    }

    private static int CalculateUsageScore(InstalledProgram program, ProgramProcessMatch match, List<string> reasons)
    {
        if (match.LastSeenUtc is null)
        {
            reasons.Add("No observed usage history yet.");

            return program.InstallDate is { } installDate && installDate < DateTime.Today.AddYears(-1)
                ? 18
                : 5;
        }

        var daysSinceLastUse = (DateTime.UtcNow - match.LastSeenUtc.Value).TotalDays;

        if (daysSinceLastUse > 365)
        {
            reasons.Add("Last observed usage is older than 1 year.");
            return 45;
        }

        if (daysSinceLastUse > 180)
        {
            reasons.Add("Last observed usage is older than 180 days.");
            return 35;
        }

        if (daysSinceLastUse > 90)
        {
            reasons.Add("Last observed usage is older than 90 days.");
            return 20;
        }

        if (daysSinceLastUse <= 14)
        {
            reasons.Add("Program appears to be used recently.");
            return 0;
        }

        return 8;
    }

    private static int CalculateSizeScore(InstalledProgram program, List<string> reasons)
    {
        if (program.EstimatedSizeMB is null or <= 0)
        {
            reasons.Add("Program size is unknown.");
            return 0;
        }

        if (program.EstimatedSizeMB > 5_000)
        {
            reasons.Add("Program occupies more than 5 GB.");
            return 30;
        }

        if (program.EstimatedSizeMB > 1_000)
        {
            reasons.Add("Program occupies more than 1 GB.");
            return 20;
        }

        if (program.EstimatedSizeMB > 250)
        {
            return 8;
        }

        return 0;
    }

    private static int CalculateSystemRiskScore(InstalledProgram program, List<string> reasons)
    {
        foreach (var publisher in RiskyPublishers)
        {
            if (!program.Publisher.Contains(publisher, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            reasons.Add($"Publisher '{publisher}' is treated as higher risk.");
            return 35;
        }

        return 0;
    }

    private static int CalculateDependencyRiskScore(InstalledProgram program, List<string> reasons)
    {
        foreach (var keyword in DependencySensitiveKeywords)
        {
            if (!program.DisplayName.Contains(keyword, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            reasons.Add($"Name contains '{keyword}', which may indicate a dependency or runtime.");
            return 40;
        }

        return 0;
    }

    private static string DetermineRecommendation(
        InstalledProgram program,
        ProgramProcessMatch match,
        int finalCleanupScore,
        int systemRiskScore,
        int dependencyRiskScore,
        List<string> reasons)
    {
        if (dependencyRiskScore >= 40)
        {
            reasons.Add("Dependency-sensitive software should not be auto-removed.");
            return "DoNotRemove";
        }

        if (systemRiskScore >= 35)
        {
            reasons.Add("System/vendor software requires manual review.");
            return "Review";
        }

        if (string.IsNullOrWhiteSpace(program.UninstallString))
        {
            reasons.Add("No uninstall command was found; manual review is required.");
            return "Review";
        }

        if (match.LastSeenUtc is { } lastSeen &&
            (DateTime.UtcNow - lastSeen).TotalDays > 180 &&
            program.EstimatedSizeMB > 1_000)
        {
            reasons.Add("Large program with no recent usage is a cleanup candidate.");
            return "CleanupCandidate";
        }

        if (finalCleanupScore >= 55)
        {
            reasons.Add("Combined score indicates meaningful cleanup potential.");
            return "CleanupCandidate";
        }

        if (finalCleanupScore >= 30)
        {
            reasons.Add("Signals are mixed; review before taking action.");
            return "Review";
        }

        if (match.LastSeenUtc is not null || finalCleanupScore < 15)
        {
            reasons.Add("Current signals favor keeping this program.");
            return "Keep";
        }

        reasons.Add("There is not enough evidence to make a stronger recommendation.");
        return "Unknown";
    }
}
