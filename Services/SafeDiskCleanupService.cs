using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic.FileIO;
using WindowsUsageCleanupAssistant.Models;

namespace WindowsUsageCleanupAssistant.Services;

public sealed class SafeDiskCleanupService : ISafeDiskCleanupService
{
    private readonly ICleanupLogService _cleanupLogService;
    private readonly string _userTempPath;
    private readonly string _windowsTempPath;
    private readonly string _thumbnailCachePath;

    public SafeDiskCleanupService(ICleanupLogService cleanupLogService)
    {
        _cleanupLogService = cleanupLogService;
        _userTempPath = Path.GetTempPath();
        _windowsTempPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "Temp");
        _thumbnailCachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "Windows",
            "Explorer");
    }

    public IReadOnlyList<CleanableCategory> Scan()
    {
        var categories = new List<CleanableCategory>
        {
            BuildDirectoryCategory(
                "user-temp",
                "User Temp",
                _userTempPath,
                "Low",
                "Temporary files in the current user's temp folder.",
                canClean: true),
            BuildDirectoryCategory(
                "windows-temp",
                "Windows Temp",
                _windowsTempPath,
                "Medium",
                "Windows temporary files. Some files may still be locked by the system.",
                canClean: true),
            BuildRecycleBinCategory(),
            BuildThumbnailCacheCategory(),
        };

        _cleanupLogService.Log($"Scan completed for {categories.Count} cleanup categories.");
        return categories;
    }

    public CleanupExecutionResult Clean(IReadOnlyList<CleanableCategory> categories)
    {
        var deletedEntries = 0;
        double reclaimedSizeMb = 0;

        foreach (var category in categories.Where(category => category.CanClean))
        {
            _cleanupLogService.Log($"Cleanup requested for category '{category.CategoryName}'.");

            switch (category.CategoryKey)
            {
                case "user-temp":
                    deletedEntries += DeleteDirectoryContents(_userTempPath, moveToRecycleBin: true);
                    reclaimedSizeMb += category.TotalSizeMB;
                    break;
                case "windows-temp":
                    deletedEntries += DeleteDirectoryContents(_windowsTempPath, moveToRecycleBin: true);
                    reclaimedSizeMb += category.TotalSizeMB;
                    break;
                case "recycle-bin":
                    EmptyRecycleBin();
                    reclaimedSizeMb += category.TotalSizeMB;
                    break;
                case "thumbnail-cache":
                    deletedEntries += DeleteThumbnailCacheFiles();
                    reclaimedSizeMb += category.TotalSizeMB;
                    break;
            }
        }

        var categoryCount = categories.Count(category => category.CanClean);
        var summary = $"Processed {categoryCount} categories. Approx. {reclaimedSizeMb:N1} MB targeted.";
        _cleanupLogService.Log(summary);

        return new CleanupExecutionResult
        {
            CategoryCount = categoryCount,
            DeletedEntries = deletedEntries,
            ReclaimedSizeMB = reclaimedSizeMb,
            Summary = summary,
        };
    }

    private CleanableCategory BuildDirectoryCategory(
        string key,
        string name,
        string directoryPath,
        string riskLevel,
        string description,
        bool canClean)
    {
        var (fileCount, totalSizeMb) = GetDirectoryStats(directoryPath);
        return new CleanableCategory
        {
            CategoryKey = key,
            CategoryName = name,
            FileCount = fileCount,
            TotalSizeMB = totalSizeMb,
            RiskLevel = riskLevel,
            Description = description,
            CanClean = canClean,
            MoveToRecycleBinWhenPossible = true,
        };
    }

    private CleanableCategory BuildRecycleBinCategory()
    {
        var recycleBinInfo = QueryRecycleBin();
        return new CleanableCategory
        {
            CategoryKey = "recycle-bin",
            CategoryName = "Recycle Bin",
            FileCount = recycleBinInfo.ItemCount > int.MaxValue ? int.MaxValue : (int)recycleBinInfo.ItemCount,
            TotalSizeMB = BytesToMb(recycleBinInfo.SizeInBytes),
            RiskLevel = "Low",
            Description = "Items already deleted by the user. Cleaning empties the Recycle Bin.",
            CanClean = true,
            MoveToRecycleBinWhenPossible = false,
        };
    }

    private CleanableCategory BuildThumbnailCacheCategory()
    {
        var thumbnailFiles = GetThumbnailCacheFiles();
        var totalBytes = thumbnailFiles.Sum(file => file.Length);

        return new CleanableCategory
        {
            CategoryKey = "thumbnail-cache",
            CategoryName = "Windows Thumbnail Cache",
            FileCount = thumbnailFiles.Count,
            TotalSizeMB = BytesToMb(totalBytes),
            RiskLevel = "Medium",
            Description = "Explorer thumbnail and icon cache files. Windows can rebuild these files.",
            CanClean = thumbnailFiles.Count > 0,
            MoveToRecycleBinWhenPossible = true,
        };
    }

    private (int FileCount, double TotalSizeMb) GetDirectoryStats(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return (0, 0);
        }

        var fileCount = 0;
        long totalBytes = 0;

        try
        {
            foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*", System.IO.SearchOption.AllDirectories))
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    totalBytes += fileInfo.Length;
                    fileCount++;
                }
                catch
                {
                }
            }
        }
        catch
        {
        }

        return (fileCount, BytesToMb(totalBytes));
    }

    private int DeleteDirectoryContents(string directoryPath, bool moveToRecycleBin)
    {
        if (!Directory.Exists(directoryPath))
        {
            return 0;
        }

        var deletedEntries = 0;

        foreach (var filePath in Directory.EnumerateFiles(directoryPath))
        {
            if (!IsSafeToDelete(filePath))
            {
                continue;
            }

            try
            {
                FileSystem.DeleteFile(
                    filePath,
                    UIOption.OnlyErrorDialogs,
                    moveToRecycleBin ? RecycleOption.SendToRecycleBin : RecycleOption.DeletePermanently);
                deletedEntries++;
                _cleanupLogService.Log($"Deleted file '{filePath}'.");
            }
            catch (Exception ex)
            {
                _cleanupLogService.Log($"Failed to delete file '{filePath}': {ex.Message}");
            }
        }

        foreach (var subDirectory in Directory.EnumerateDirectories(directoryPath))
        {
            if (!IsSafeToDelete(subDirectory))
            {
                continue;
            }

            try
            {
                FileSystem.DeleteDirectory(
                    subDirectory,
                    UIOption.OnlyErrorDialogs,
                    moveToRecycleBin ? RecycleOption.SendToRecycleBin : RecycleOption.DeletePermanently);
                deletedEntries++;
                _cleanupLogService.Log($"Deleted directory '{subDirectory}'.");
            }
            catch (Exception ex)
            {
                _cleanupLogService.Log($"Failed to delete directory '{subDirectory}': {ex.Message}");
            }
        }

        return deletedEntries;
    }

    private int DeleteThumbnailCacheFiles()
    {
        var deletedEntries = 0;

        foreach (var fileInfo in GetThumbnailCacheFiles())
        {
            if (!IsSafeToDelete(fileInfo.FullName))
            {
                continue;
            }

            try
            {
                FileSystem.DeleteFile(
                    fileInfo.FullName,
                    UIOption.OnlyErrorDialogs,
                    RecycleOption.SendToRecycleBin);
                deletedEntries++;
                _cleanupLogService.Log($"Deleted thumbnail cache file '{fileInfo.FullName}'.");
            }
            catch (Exception ex)
            {
                _cleanupLogService.Log($"Failed to delete thumbnail cache file '{fileInfo.FullName}': {ex.Message}");
            }
        }

        return deletedEntries;
    }

    private List<FileInfo> GetThumbnailCacheFiles()
    {
        if (!Directory.Exists(_thumbnailCachePath))
        {
            return [];
        }

        try
        {
            return Directory.EnumerateFiles(_thumbnailCachePath, "*cache*.db", System.IO.SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private void EmptyRecycleBin()
    {
        const int noConfirmation = 0x00000001;
        const int noProgressUi = 0x00000002;
        const int noSound = 0x00000004;

        var result = SHEmptyRecycleBin(IntPtr.Zero, null, noConfirmation | noProgressUi | noSound);
        if (result != 0)
        {
            throw new InvalidOperationException($"Recycle Bin cleanup failed with HRESULT {result}.");
        }

        _cleanupLogService.Log("Recycle Bin emptied.");
    }

    private bool IsSafeToDelete(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var disallowedRoots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads",
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Path.GetFullPath(AppContext.BaseDirectory),
        }
        .Where(root => !string.IsNullOrWhiteSpace(root))
        .Select(root => Path.GetFullPath(root))
        .ToArray();

        var allowed = fullPath.StartsWith(Path.GetFullPath(_userTempPath), StringComparison.OrdinalIgnoreCase) ||
                      fullPath.StartsWith(Path.GetFullPath(_windowsTempPath), StringComparison.OrdinalIgnoreCase) ||
                      fullPath.StartsWith(Path.GetFullPath(_thumbnailCachePath), StringComparison.OrdinalIgnoreCase);

        if (!allowed)
        {
            return false;
        }

        return !disallowedRoots.Any(root =>
            fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
            !fullPath.StartsWith(Path.GetFullPath(_userTempPath), StringComparison.OrdinalIgnoreCase) &&
            !fullPath.StartsWith(Path.GetFullPath(_windowsTempPath), StringComparison.OrdinalIgnoreCase) &&
            !fullPath.StartsWith(Path.GetFullPath(_thumbnailCachePath), StringComparison.OrdinalIgnoreCase));
    }

    private static RecycleBinInfo QueryRecycleBin()
    {
        var info = new SHQUERYRBINFO
        {
            cbSize = Marshal.SizeOf<SHQUERYRBINFO>(),
        };

        var result = SHQueryRecycleBin(null, ref info);
        return result == 0
            ? new RecycleBinInfo(info.i64NumItems, info.i64Size)
            : new RecycleBinInfo(0, 0);
    }

    private static double BytesToMb(long bytes)
    {
        return Math.Round(bytes / 1024d / 1024d, 1);
    }

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, int dwFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    private sealed record RecycleBinInfo(long ItemCount, long SizeInBytes);
}
