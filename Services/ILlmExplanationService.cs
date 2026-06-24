using WindowsUsageCleanupAssistant.Models;

namespace WindowsUsageCleanupAssistant.Services;

public interface ILlmExplanationService
{
    Task<string> GenerateExplanationAsync(ProgramAnalysisDto analysis, CancellationToken cancellationToken = default);
}
