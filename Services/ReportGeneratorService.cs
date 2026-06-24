using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using WindowsUsageCleanupAssistant.Models;

namespace WindowsUsageCleanupAssistant.Services;

public sealed class ReportGeneratorService : IReportGeneratorService
{
    private readonly string _reportsDirectoryPath;

    public ReportGeneratorService(string reportsDirectoryPath)
    {
        Directory.CreateDirectory(reportsDirectoryPath);
        _reportsDirectoryPath = reportsDirectoryPath;
    }

    public string ExportHtml(ReportSnapshot snapshot)
    {
        var timestamp = snapshot.GeneratedAtUtc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var filePath = Path.Combine(_reportsDirectoryPath, $"usage-cleanup-report-{timestamp}.html");
        File.WriteAllText(filePath, BuildHtml(snapshot), Encoding.UTF8);
        return filePath;
    }

    public string ExportCsv(ReportSnapshot snapshot)
    {
        var timestamp = snapshot.GeneratedAtUtc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var filePath = Path.Combine(_reportsDirectoryPath, $"usage-cleanup-report-{timestamp}.csv");

        var builder = new StringBuilder();
        builder.AppendLine("DisplayName,Publisher,Version,EstimatedSizeMB,LastUsedUtc,MatchedProcessName,MatchConfidence,UsageScore,SizeScore,SystemRiskScore,DependencyRiskScore,FinalCleanupScore,Recommendation,RecommendationReasons");

        foreach (var program in snapshot.Programs.OrderBy(program => program.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            builder.AppendLine(string.Join(",",
                Csv(program.DisplayName),
                Csv(program.Publisher),
                Csv(program.DisplayVersion),
                Csv(program.EstimatedSizeMB?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                Csv(program.LastUsedUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty),
                Csv(program.MatchedProcessName),
                Csv(program.MatchConfidence.ToString(CultureInfo.InvariantCulture)),
                Csv(program.UsageScore.ToString(CultureInfo.InvariantCulture)),
                Csv(program.SizeScore.ToString(CultureInfo.InvariantCulture)),
                Csv(program.SystemRiskScore.ToString(CultureInfo.InvariantCulture)),
                Csv(program.DependencyRiskScore.ToString(CultureInfo.InvariantCulture)),
                Csv(program.FinalCleanupScore.ToString(CultureInfo.InvariantCulture)),
                Csv(program.Recommendation),
                Csv(program.RecommendationReasons)));
        }

        File.WriteAllText(filePath, builder.ToString(), Encoding.UTF8);
        return filePath;
    }

    public string ExportJson(ReportSnapshot snapshot)
    {
        var timestamp = snapshot.GeneratedAtUtc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var filePath = Path.Combine(_reportsDirectoryPath, $"usage-cleanup-report-{timestamp}.json");
        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        File.WriteAllText(filePath, json, Encoding.UTF8);
        return filePath;
    }

    private static string BuildHtml(ReportSnapshot snapshot)
    {
        var summary = snapshot.Summary;
        var programs = snapshot.Programs.OrderBy(program => program.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
        var unusedPrograms = programs
            .Where(program => program.LastUsedUtc is null || (snapshot.GeneratedAtUtc - program.LastUsedUtc.Value).TotalDays > 180)
            .OrderByDescending(program => program.EstimatedSizeMB ?? 0)
            .ToList();
        var cleanupCandidates = programs
            .Where(program => string.Equals(program.Recommendation, "CleanupCandidate", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(program => program.FinalCleanupScore)
            .ToList();
        var dependencySensitive = programs
            .Where(program => program.DependencyRiskScore > 0 || string.Equals(program.Recommendation, "DoNotRemove", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(program => program.DependencyRiskScore)
            .ToList();
        var largestPrograms = programs
            .OrderByDescending(program => program.EstimatedSizeMB ?? 0)
            .Take(10)
            .ToList();
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"en\">");
        builder.AppendLine("<head>");
        builder.AppendLine("  <meta charset=\"utf-8\" />");
        builder.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        builder.AppendLine("  <title>Windows Usage & Cleanup Report</title>");
        builder.AppendLine("  <style>");
        builder.AppendLine("    :root { --bg: #f4f1ea; --panel: #fffdf8; --ink: #1f2a30; --muted: #667780; --line: #d9d2c7; --accent: #17494d; --accent-soft: #d7ebe3; --warn: #a85b20; --danger: #8b3232; --ok: #2e6a4f; }");
        builder.AppendLine("    * { box-sizing: border-box; }");
        builder.AppendLine("    body { margin: 0; font-family: 'Segoe UI', 'Helvetica Neue', sans-serif; background: linear-gradient(180deg, #f5f0e8 0%, #eef4f3 100%); color: var(--ink); }");
        builder.AppendLine("    .page { max-width: 1380px; margin: 0 auto; padding: 28px; }");
        builder.AppendLine("    .hero, .section { background: var(--panel); border: 1px solid var(--line); border-radius: 22px; box-shadow: 0 8px 22px rgba(18, 42, 48, 0.05); }");
        builder.AppendLine("    .hero { padding: 28px; box-shadow: 0 12px 28px rgba(18, 42, 48, 0.08); }");
        builder.AppendLine("    .section { margin-top: 22px; padding: 22px; }");
        builder.AppendLine("    h1, h2 { margin: 0 0 12px; }");
        builder.AppendLine("    p { margin: 0; color: var(--muted); }");
        builder.AppendLine("    .cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 14px; margin-top: 22px; }");
        builder.AppendLine("    .card { background: var(--accent-soft); border-radius: 18px; padding: 18px; border: 1px solid rgba(23, 73, 77, 0.1); }");
        builder.AppendLine("    .card .label { display: block; font-size: 12px; text-transform: uppercase; letter-spacing: 0.08em; color: var(--muted); margin-bottom: 8px; }");
        builder.AppendLine("    .card .value { font-size: 30px; font-weight: 700; color: var(--accent); }");
        builder.AppendLine("    .grid-two { display: grid; grid-template-columns: 1fr 1fr; gap: 18px; }");
        builder.AppendLine("    table { width: 100%; border-collapse: collapse; margin-top: 14px; font-size: 14px; }");
        builder.AppendLine("    th, td { text-align: left; padding: 10px 12px; border-bottom: 1px solid #e7e0d5; vertical-align: top; }");
        builder.AppendLine("    th { font-size: 12px; text-transform: uppercase; letter-spacing: 0.06em; color: var(--muted); background: #faf7f1; }");
        builder.AppendLine("    .pill { display: inline-block; border-radius: 999px; padding: 4px 10px; font-size: 12px; font-weight: 600; }");
        builder.AppendLine("    .CleanupCandidate { background: #ffe2c8; color: var(--warn); }");
        builder.AppendLine("    .DoNotRemove { background: #f7d7d7; color: var(--danger); }");
        builder.AppendLine("    .Keep { background: #d7efdf; color: var(--ok); }");
        builder.AppendLine("    .Review, .Unknown { background: #e6ecef; color: #4e636d; }");
        builder.AppendLine("    @media (max-width: 900px) { .grid-two { grid-template-columns: 1fr; } .page { padding: 16px; } }");
        builder.AppendLine("  </style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.AppendLine("  <div class=\"page\">");
        builder.AppendLine("    <section class=\"hero\">");
        builder.AppendLine("      <h1>Windows Usage & Cleanup Report</h1>");
        builder.AppendLine($"      <p>Generated at {Html(snapshot.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture))}</p>");
        builder.AppendLine("      <div class=\"cards\">");
        builder.AppendLine($"        {BuildCard("Installed Programs", summary.TotalInstalledPrograms.ToString(CultureInfo.InvariantCulture))}");
        builder.AppendLine($"        {BuildCard("Unused 6+ Months", summary.UnusedSixMonthsCount.ToString(CultureInfo.InvariantCulture))}");
        builder.AppendLine($"        {BuildCard("Cleanup Candidates", summary.CleanupCandidateCount.ToString(CultureInfo.InvariantCulture))}");
        builder.AppendLine($"        {BuildCard("Dependency Sensitive", summary.DependencySensitiveCount.ToString(CultureInfo.InvariantCulture))}");
        builder.AppendLine($"        {BuildCard("Temp Cleanup Size", $"{summary.TemporaryCleanupSizeMB:N1} MB")}");
        builder.AppendLine($"        {BuildCard("Estimated Reclaimable", $"{summary.EstimatedReclaimableSizeMB:N1} MB")}");
        builder.AppendLine("      </div>");
        builder.AppendLine("    </section>");
        builder.AppendLine("    <section class=\"section\">");
        builder.AppendLine("      <h2>Largest Programs</h2>");
        builder.AppendLine(BuildProgramTable(largestPrograms));
        builder.AppendLine("    </section>");
        builder.AppendLine("    <div class=\"grid-two\">");
        builder.AppendLine("      <section class=\"section\">");
        builder.AppendLine("        <h2>Unused Programs</h2>");
        builder.AppendLine(BuildProgramTable(unusedPrograms));
        builder.AppendLine("      </section>");
        builder.AppendLine("      <section class=\"section\">");
        builder.AppendLine("        <h2>Cleanup Candidates</h2>");
        builder.AppendLine(BuildProgramTable(cleanupCandidates));
        builder.AppendLine("      </section>");
        builder.AppendLine("    </div>");
        builder.AppendLine("    <div class=\"grid-two\">");
        builder.AppendLine("      <section class=\"section\">");
        builder.AppendLine("        <h2>Dependency-Sensitive Programs</h2>");
        builder.AppendLine(BuildProgramTable(dependencySensitive));
        builder.AppendLine("      </section>");
        builder.AppendLine("      <section class=\"section\">");
        builder.AppendLine("        <h2>Disk Cleanup Preview</h2>");
        builder.AppendLine(BuildCleanupTable(snapshot.CleanupPreview));
        builder.AppendLine("      </section>");
        builder.AppendLine("    </div>");
        builder.AppendLine("    <section class=\"section\">");
        builder.AppendLine("      <h2>Installed Programs</h2>");
        builder.AppendLine(BuildProgramTable(programs));
        builder.AppendLine("    </section>");
        builder.AppendLine("  </div>");
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");
        return builder.ToString();
    }

    private static string BuildProgramTable(IEnumerable<ReportProgramItem> programs)
    {
        var rows = programs.ToList();
        if (rows.Count == 0)
        {
            return "<p>No data available for this section.</p>";
        }

        var builder = new StringBuilder();
        builder.AppendLine("<table><thead><tr><th>Name</th><th>Recommendation</th><th>Score</th><th>Last Used</th><th>Size MB</th><th>Process</th><th>Reasons</th></tr></thead><tbody>");

        foreach (var program in rows)
        {
            builder.AppendLine(
                $"<tr><td>{Html(program.DisplayName)}<br /><small>{Html(program.Publisher)}</small></td>" +
                $"<td><span class=\"pill {Html(program.Recommendation)}\">{Html(program.Recommendation)}</span></td>" +
                $"<td>{program.FinalCleanupScore}</td>" +
                $"<td>{Html(program.LastUsedUtc?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "No usage data")}</td>" +
                $"<td>{Html(program.EstimatedSizeMB?.ToString("N0", CultureInfo.InvariantCulture) ?? "-")}</td>" +
                $"<td>{Html(program.MatchedProcessName)}</td>" +
                $"<td>{Html(program.RecommendationReasons)}</td></tr>");
        }

        builder.AppendLine("</tbody></table>");
        return builder.ToString();
    }

    private static string BuildCleanupTable(IReadOnlyList<ReportCleanupItem> items)
    {
        if (items.Count == 0)
        {
            return "<p>No cleanup scan data is available yet.</p>";
        }

        var builder = new StringBuilder();
        builder.AppendLine("<table><thead><tr><th>Category</th><th>Files</th><th>Size MB</th><th>Risk</th><th>Description</th></tr></thead><tbody>");

        foreach (var item in items)
        {
            builder.AppendLine(
                $"<tr><td>{Html(item.CategoryName)}</td><td>{item.FileCount}</td><td>{item.TotalSizeMB:N1}</td><td>{Html(item.RiskLevel)}</td><td>{Html(item.Description)}</td></tr>");
        }

        builder.AppendLine("</tbody></table>");
        return builder.ToString();
    }

    private static string BuildCard(string label, string value)
    {
        return $"<div class=\"card\"><span class=\"label\">{Html(label)}</span><span class=\"value\">{Html(value)}</span></div>";
    }

    private static string Csv(string value)
    {
        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string Html(string value)
    {
        return System.Net.WebUtility.HtmlEncode(value);
    }
}
