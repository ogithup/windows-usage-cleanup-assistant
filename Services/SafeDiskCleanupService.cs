using System.IO;
using System.Runtime.InteropServices;
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

    public CleanupExecutionResult Clean(
        IReadOnlyList<CleanableCategory> categories,
        bool skipLockedFilesAutomatically,
        IProgress<CleanupProgressUpdate>? progress = null)
    {
        var deletedEntries = 0;
        var skippedEntries = 0;
        double reclaimedSizeMb = 0;
        var cleanableCategories = categories.Where(category => category.CanClean).ToList();
        var totalSteps = CalculateTotalSteps(cleanableCategories);
        var processedSteps = 0;

        foreach (var category in cleanableCategories)
        {
            _cleanupLogService.Log($"Cleanup requested for category '{category.CategoryName}'.");
            ReportProgress(progress, ++processedSteps, totalSteps, $"Scanning {category.CategoryName}", category.CategoryName, deletedEntries, skippedEntries);

            switch (category.CategoryKey)
            {
                case "user-temp":
                    deletedEntries += DeleteDirectoryContents(_userTempPath, moveToRecycleBin: true, skipLockedFilesAutomatically, ref skippedEntries, ref processedSteps, totalSteps, progress, category.CategoryName);
                    reclaimedSizeMb += category.TotalSizeMB;
                    break;
                case "windows-temp":
                    deletedEntries += DeleteDirectoryContents(_windowsTempPath, moveToRecycleBin: true, skipLockedFilesAutomatically, ref skippedEntries, ref processedSteps, totalSteps, progress, category.CategoryName);
                    reclaimedSizeMb += category.TotalSizeMB;
                    break;
                case "recycle-bin":
                    EmptyRecycleBin();
                    ReportProgress(progress, ++processedSteps, totalSteps, "Recycle Bin emptied", category.CategoryName, deletedEntries, skippedEntries);
                    reclaimedSizeMb += category.TotalSizeMB;
                    break;
                case "thumbnail-cache":
                    deletedEntries += DeleteThumbnailCacheFiles(skipLockedFilesAutomatically, ref skippedEntries, ref processedSteps, totalSteps, progress, category.CategoryName);
                    reclaimedSizeMb += category.TotalSizeMB;
                    break;
            }
        }

        var categoryCount = cleanableCategories.Count;
        var summary = $"Processed {categoryCount} categories. Approx. {reclaimedSizeMb:N1} MB targeted. Skipped {skippedEntries} locked/in-use entries.";
        _cleanupLogService.Log(summary);
        ReportProgress(progress, totalSteps, totalSteps, "Cleanup complete", summary, deletedEntries, skippedEntries);

        return new CleanupExecutionResult
        {
            CategoryCount = categoryCount,
            DeletedEntries = deletedEntries,
            SkippedEntries = skippedEntries,
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

    private int DeleteDirectoryContents(
        string directoryPath,
        bool moveToRecycleBin,
        bool skipLockedFilesAutomatically,
        ref int skippedEntries,
        ref int processedSteps,
        int totalSteps,
        IProgress<CleanupProgressUpdate>? progress,
        string categoryName)
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
                DeletePathSilently(filePath, moveToRecycleBin);
                deletedEntries++;
                _cleanupLogService.Log($"Deleted file '{filePath}'.");
                ReportProgress(progress, ++processedSteps, totalSteps, $"Deleted file in {categoryName}", Path.GetFileName(filePath), deletedEntries, skippedEntries);
            }
            catch (Exception ex)
            {
                skippedEntries++;
                _cleanupLogService.Log($"Failed to delete file '{filePath}': {ex.Message}");
                ReportProgress(progress, ++processedSteps, totalSteps, $"Skipped locked file in {categoryName}", Path.GetFileName(filePath), deletedEntries, skippedEntries);
                if (!skipLockedFilesAutomatically)
                {
                    throw new IOException($"Cleanup stopped on locked or inaccessible file: {filePath}", ex);
                }
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
                DeletePathSilently(subDirectory, moveToRecycleBin);
                deletedEntries++;
                _cleanupLogService.Log($"Deleted directory '{subDirectory}'.");
                ReportProgress(progress, ++processedSteps, totalSteps, $"Deleted directory in {categoryName}", Path.GetFileName(subDirectory), deletedEntries, skippedEntries);
            }
            catch (Exception ex)
            {
                skippedEntries++;
                _cleanupLogService.Log($"Failed to delete directory '{subDirectory}': {ex.Message}");
                ReportProgress(progress, ++processedSteps, totalSteps, $"Skipped locked directory in {categoryName}", Path.GetFileName(subDirectory), deletedEntries, skippedEntries);
                if (!skipLockedFilesAutomatically)
                {
                    throw new IOException($"Cleanup stopped on locked or inaccessible directory: {subDirectory}", ex);
                }
            }
        }

        return deletedEntries;
    }

    private int DeleteThumbnailCacheFiles(
        bool skipLockedFilesAutomatically,
        ref int skippedEntries,
        ref int processedSteps,
        int totalSteps,
        IProgress<CleanupProgressUpdate>? progress,
        string categoryName)
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
                DeletePathSilently(fileInfo.FullName, moveToRecycleBin: true);
                deletedEntries++;
                _cleanupLogService.Log($"Deleted thumbnail cache file '{fileInfo.FullName}'.");
                ReportProgress(progress, ++processedSteps, totalSteps, $"Deleted file in {categoryName}", fileInfo.Name, deletedEntries, skippedEntries);
            }
            catch (Exception ex)
            {
                skippedEntries++;
                _cleanupLogService.Log($"Failed to delete thumbnail cache file '{fileInfo.FullName}': {ex.Message}");
                ReportProgress(progress, ++processedSteps, totalSteps, $"Skipped locked file in {categoryName}", fileInfo.Name, deletedEntries, skippedEntries);
                if (!skipLockedFilesAutomatically)
                {
                    throw new IOException($"Cleanup stopped on locked or inaccessible thumbnail cache file: {fileInfo.FullName}", ex);
                }
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

    private static int CalculateTotalSteps(IReadOnlyList<CleanableCategory> categories)
    {
        var total = 0;
        foreach (var category in categories)
        {
            total += 1;
            total += Math.Max(category.FileCount, 1);
        }

        return Math.Max(total, 1);
    }

    private static void ReportProgress(
        IProgress<CleanupProgressUpdate>? progress,
        int processedSteps,
        int totalSteps,
        string currentStep,
        string currentItem,
        int deletedEntries,
        int skippedEntries)
    {
        progress?.Report(new CleanupProgressUpdate
        {
            ProcessedSteps = Math.Min(processedSteps, totalSteps),
            TotalSteps = totalSteps,
            CurrentStep = currentStep,
            CurrentItem = currentItem,
            DeletedEntries = deletedEntries,
            SkippedEntries = skippedEntries,
        });
    }

    private static void DeletePathSilently(string path, bool moveToRecycleBin)
    {
        var fileOperation = new SHFILEOPSTRUCT
        {
            wFunc = FileOperationType.Delete,
            pFrom = path + "\0\0",
            fFlags = FileOperationFlags.NoConfirmation |
                     FileOperationFlags.NoErrorUi |
                     FileOperationFlags.Silent |
                     (moveToRecycleBin ? FileOperationFlags.AllowUndo : FileOperationFlags.None),
        };

        var result = SHFileOperation(ref fileOperation);
        if (result != 0 || fileOperation.fAnyOperationsAborted)
        {
            throw new IOException($"Shell delete failed for '{path}' (code {result}).");
        }
    }

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, int dwFlags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public FileOperationType wFunc;
        [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pTo;
        public FileOperationFlags fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszProgressTitle;
    }

    private enum FileOperationType : uint
    {
        Delete = 3,
    }

    [Flags]
    private enum FileOperationFlags : ushort
    {
        None = 0x0000,
        Silent = 0x0004,
        NoConfirmation = 0x0010,
        AllowUndo = 0x0040,
        NoErrorUi = 0x0400,
    }

    private sealed record RecycleBinInfo(long ItemCount, long SizeInBytes);
}
