using WindowsUsageCleanupAssistant.Models;

namespace WindowsUsageCleanupAssistant.Services;

public interface ISafeDiskCleanupService
{
    IReadOnlyList<CleanableCategory> Scan();

    CleanupExecutionResult Clean(
        IReadOnlyList<CleanableCategory> categories,
        bool skipLockedFilesAutomatically,
        IProgress<CleanupProgressUpdate>? progress = null);
}
