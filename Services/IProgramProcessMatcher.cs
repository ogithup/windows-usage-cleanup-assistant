using WindowsUsageCleanupAssistant.Models;

namespace WindowsUsageCleanupAssistant.Services;

public interface IProgramProcessMatcher
{
    ProgramProcessMatch Match(InstalledProgram program, IReadOnlyList<UsageRecord> usageRecords);
}
