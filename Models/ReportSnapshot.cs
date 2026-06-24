namespace WindowsUsageCleanupAssistant.Models;

public sealed class ReportSnapshot
{
    public DateTime GeneratedAtUtc { get; init; }

    public ReportSummary Summary { get; init; } = new();

    public IReadOnlyList<ReportProgramItem> Programs { get; init; } = [];

    public IReadOnlyList<ReportCleanupItem> CleanupPreview { get; init; } = [];
}
