namespace WindowsUsageCleanupAssistant.Models;

public sealed class InstalledProgram
{
    public string DisplayName { get; init; } = string.Empty;

    public string Publisher { get; init; } = string.Empty;

    public string DisplayVersion { get; init; } = string.Empty;

    public string InstallLocation { get; init; } = string.Empty;

    public long? EstimatedSizeMB { get; init; }

    public DateTime? InstallDate { get; init; }

    public string UninstallString { get; init; } = string.Empty;

    public string RegistryPath { get; init; } = string.Empty;
}
