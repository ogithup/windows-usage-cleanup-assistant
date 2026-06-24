using WindowsUsageCleanupAssistant.Models;

namespace WindowsUsageCleanupAssistant.Services;

public interface IReportGeneratorService
{
    string ExportHtml(ReportSnapshot snapshot);

    string ExportCsv(ReportSnapshot snapshot);

    string ExportJson(ReportSnapshot snapshot);
}
