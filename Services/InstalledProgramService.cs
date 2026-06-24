using Microsoft.Win32;
using WindowsUsageCleanupAssistant.Models;

namespace WindowsUsageCleanupAssistant.Services;

public sealed class InstalledProgramService : IInstalledProgramService
{
    private static readonly (RegistryHive Hive, RegistryView View, string Path)[] RegistryLocations =
    [
        (RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\Uninstall"),
        (RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
        (RegistryHive.CurrentUser, RegistryView.Default, @"Software\Microsoft\Windows\CurrentVersion\Uninstall"),
    ];

    public IReadOnlyList<InstalledProgram> GetInstalledPrograms()
    {
        var programs = new List<InstalledProgram>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var location in RegistryLocations)
        {
            using var baseKey = RegistryKey.OpenBaseKey(location.Hive, location.View);
            using var uninstallKey = baseKey.OpenSubKey(location.Path);

            if (uninstallKey is null)
            {
                continue;
            }

            foreach (var subKeyName in uninstallKey.GetSubKeyNames())
            {
                using var programKey = uninstallKey.OpenSubKey(subKeyName);
                if (programKey is null)
                {
                    continue;
                }

                var displayName = ReadString(programKey, "DisplayName");
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    continue;
                }

                var registryPath = $@"{location.Hive}\{location.Path}\{subKeyName}";
                if (!seenKeys.Add(registryPath))
                {
                    continue;
                }

                programs.Add(new InstalledProgram
                {
                    DisplayName = displayName,
                    Publisher = ReadString(programKey, "Publisher"),
                    DisplayVersion = ReadString(programKey, "DisplayVersion"),
                    InstallLocation = ReadString(programKey, "InstallLocation"),
                    EstimatedSizeMB = ParseEstimatedSizeMb(programKey.GetValue("EstimatedSize")),
                    InstallDate = ParseInstallDate(ReadString(programKey, "InstallDate")),
                    UninstallString = ReadString(programKey, "UninstallString"),
                    RegistryPath = registryPath,
                });
            }
        }

        return programs
            .OrderBy(program => program.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static string ReadString(RegistryKey key, string valueName)
    {
        return key.GetValue(valueName)?.ToString()?.Trim() ?? string.Empty;
    }

    private static long? ParseEstimatedSizeMb(object? rawValue)
    {
        if (rawValue is null)
        {
            return null;
        }

        if (!long.TryParse(rawValue.ToString(), out var sizeInKb) || sizeInKb <= 0)
        {
            return null;
        }

        return (long)Math.Ceiling(sizeInKb / 1024d);
    }

    private static DateTime? ParseInstallDate(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue) || rawValue.Length != 8)
        {
            return null;
        }

        if (!int.TryParse(rawValue[..4], out var year) ||
            !int.TryParse(rawValue.Substring(4, 2), out var month) ||
            !int.TryParse(rawValue.Substring(6, 2), out var day))
        {
            return null;
        }

        try
        {
            return new DateTime(year, month, day);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
