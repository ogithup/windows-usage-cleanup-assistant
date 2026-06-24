using WindowsUsageCleanupAssistant.Models;

namespace WindowsUsageCleanupAssistant.Services;

public interface ICleanupRecommendationEngine
{
    CleanupRecommendation Evaluate(InstalledProgram program, ProgramProcessMatch match);
}
