using WindowsUsageCleanupAssistant.Models;

namespace WindowsUsageCleanupAssistant.Services;

public interface IInstalledProgramService
{
    IReadOnlyList<InstalledProgram> GetInstalledPrograms();
}
