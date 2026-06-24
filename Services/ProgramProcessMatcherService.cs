using System.IO;
using System.Text.RegularExpressions;
using WindowsUsageCleanupAssistant.Models;

namespace WindowsUsageCleanupAssistant.Services;

public sealed class ProgramProcessMatcherService : IProgramProcessMatcher
{
    private static readonly Dictionary<string, string[]> KnownAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Google Chrome"] = ["chrome.exe"],
        ["Visual Studio Code"] = ["code.exe"],
        ["Android Studio"] = ["studio64.exe", "studio.exe"],
        ["Microsoft Edge"] = ["msedge.exe"],
        ["Mozilla Firefox"] = ["firefox.exe"],
        ["Unity"] = ["unity.exe", "unityhub.exe"],
        ["Discord"] = ["discord.exe"],
        ["Steam"] = ["steam.exe"],
    };

    public ProgramProcessMatch Match(InstalledProgram program, IReadOnlyList<UsageRecord> usageRecords)
    {
        var bestMatch = new ProgramProcessMatch();

        foreach (var usageRecord in usageRecords)
        {
            var score = CalculateScore(program, usageRecord);
            if (score <= bestMatch.ConfidenceScore)
            {
                continue;
            }

            bestMatch = new ProgramProcessMatch
            {
                MatchedProcessName = usageRecord.ProcessName,
                MatchedExecutablePath = usageRecord.ExecutablePath,
                ConfidenceScore = score,
                FirstSeenUtc = usageRecord.FirstSeenUtc,
                LastSeenUtc = usageRecord.LastSeenUtc,
                TotalObservedMinutes = usageRecord.TotalObservedMinutes,
                LaunchCount = usageRecord.LaunchCount,
            };
        }

        return bestMatch;
    }

    private static int CalculateScore(InstalledProgram program, UsageRecord usageRecord)
    {
        var score = 0;
        var normalizedDisplayName = Normalize(program.DisplayName);
        var normalizedPublisher = Normalize(program.Publisher);
        var normalizedProcessName = Normalize(Path.GetFileNameWithoutExtension(usageRecord.ProcessName));
        var normalizedExecutableName = Normalize(Path.GetFileNameWithoutExtension(usageRecord.ExecutablePath));
        var normalizedExecutablePath = NormalizePath(usageRecord.ExecutablePath);
        var normalizedInstallLocation = NormalizePath(program.InstallLocation);

        if (!string.IsNullOrWhiteSpace(normalizedInstallLocation) &&
            !string.IsNullOrWhiteSpace(normalizedExecutablePath) &&
            normalizedExecutablePath.Contains(normalizedInstallLocation, StringComparison.OrdinalIgnoreCase))
        {
            score += 55;
        }

        if (IsKnownAliasMatch(program.DisplayName, usageRecord.ProcessName))
        {
            score += 30;
        }

        score += ScaleSimilarity(normalizedDisplayName, normalizedProcessName, 25);
        score += ScaleSimilarity(normalizedDisplayName, normalizedExecutableName, 20);
        score += ScaleSimilarity(normalizedPublisher, normalizedProcessName, 10);

        if (!string.IsNullOrWhiteSpace(normalizedDisplayName) &&
            !string.IsNullOrWhiteSpace(normalizedProcessName) &&
            (normalizedDisplayName.Contains(normalizedProcessName, StringComparison.OrdinalIgnoreCase) ||
             normalizedProcessName.Contains(normalizedDisplayName, StringComparison.OrdinalIgnoreCase)))
        {
            score += 10;
        }

        return Math.Clamp(score, 0, 100);
    }

    private static bool IsKnownAliasMatch(string displayName, string processName)
    {
        if (!KnownAliases.TryGetValue(displayName.Trim(), out var aliases))
        {
            return false;
        }

        return aliases.Any(alias => string.Equals(alias, processName, StringComparison.OrdinalIgnoreCase));
    }

    private static int ScaleSimilarity(string left, string right, int maxScore)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return 0;
        }

        var similarity = CalculateTokenSimilarity(left, right);
        return (int)Math.Round(similarity * maxScore, MidpointRounding.AwayFromZero);
    }

    private static double CalculateTokenSimilarity(string left, string right)
    {
        var leftTokens = Tokenize(left);
        var rightTokens = Tokenize(right);
        if (leftTokens.Count == 0 || rightTokens.Count == 0)
        {
            return 0;
        }

        var intersectionCount = leftTokens.Intersect(rightTokens, StringComparer.OrdinalIgnoreCase).Count();
        var unionCount = leftTokens.Union(rightTokens, StringComparer.OrdinalIgnoreCase).Count();
        return unionCount == 0 ? 0 : (double)intersectionCount / unionCount;
    }

    private static HashSet<string> Tokenize(string input)
    {
        return Regex.Split(input, "[^a-z0-9]+", RegexOptions.IgnoreCase)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(token => token.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static string NormalizePath(string value)
    {
        return value.Trim().Replace('/', '\\').ToLowerInvariant();
    }
}
