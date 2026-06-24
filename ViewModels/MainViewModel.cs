using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using WindowsUsageCleanupAssistant.Commands;
using WindowsUsageCleanupAssistant.Models;
using WindowsUsageCleanupAssistant.Services;

namespace WindowsUsageCleanupAssistant.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IInstalledProgramService _installedProgramService;
    private readonly IUsageRepository _usageRepository;
    private readonly IProgramUsageTracker _programUsageTracker;
    private readonly IProgramProcessMatcher _programProcessMatcher;
    private readonly ICleanupRecommendationEngine _cleanupRecommendationEngine;
    private readonly ISafeDiskCleanupService _safeDiskCleanupService;
    private readonly ICleanupLogService _cleanupLogService;
    private readonly IReportGeneratorService _reportGeneratorService;
    private readonly ObservableCollection<InstalledProgramViewModel> _programs = [];
    private readonly ObservableCollection<UsageRecord> _usageRecords = [];
    private readonly ObservableCollection<CleanableCategoryViewModel> _cleanableCategories = [];
    private readonly RelayCommand _refreshInventoryCommand;
    private readonly RelayCommand _refreshUsageCommand;
    private readonly RelayCommand _scanCleanupCommand;
    private readonly RelayCommand _previewCleanupCommand;
    private readonly RelayCommand _cleanSelectedCommand;
    private readonly RelayCommand _generateCleanupReportCommand;
    private readonly RelayCommand _exportHtmlReportCommand;
    private readonly RelayCommand _exportCsvReportCommand;
    private readonly RelayCommand _exportJsonReportCommand;
    private string _searchText = string.Empty;
    private string _inventoryStatusText = "Ready.";
    private string _usageStatusText = "Usage tracking is starting.";
    private string _cleanupStatusText = "Scan cleanable files to preview safe cleanup candidates.";
    private string _reportStatusText = "Export reports in HTML, CSV, or JSON.";
    private bool _isInventoryLoading;
    private bool _isUsageLoading;
    private bool _isCleanupBusy;

    public MainViewModel(
        IInstalledProgramService installedProgramService,
        IUsageRepository usageRepository,
        IProgramUsageTracker programUsageTracker,
        IProgramProcessMatcher programProcessMatcher,
        ICleanupRecommendationEngine cleanupRecommendationEngine,
        ISafeDiskCleanupService safeDiskCleanupService,
        ICleanupLogService cleanupLogService,
        IReportGeneratorService reportGeneratorService)
    {
        _installedProgramService = installedProgramService;
        _usageRepository = usageRepository;
        _programUsageTracker = programUsageTracker;
        _programProcessMatcher = programProcessMatcher;
        _cleanupRecommendationEngine = cleanupRecommendationEngine;
        _safeDiskCleanupService = safeDiskCleanupService;
        _cleanupLogService = cleanupLogService;
        _reportGeneratorService = reportGeneratorService;

        ProgramsView = CollectionViewSource.GetDefaultView(_programs);
        ProgramsView.Filter = FilterProgram;

        UsageView = CollectionViewSource.GetDefaultView(_usageRecords);
        CleanupView = CollectionViewSource.GetDefaultView(_cleanableCategories);

        _refreshInventoryCommand = new RelayCommand(LoadPrograms, () => !IsInventoryLoading);
        _refreshUsageCommand = new RelayCommand(LoadUsageRecords, () => !IsUsageLoading);
        _scanCleanupCommand = new RelayCommand(ScanCleanupTargets, () => !IsCleanupBusy);
        _previewCleanupCommand = new RelayCommand(PreviewCleanupTargets, () => !IsCleanupBusy && _cleanableCategories.Count > 0);
        _cleanSelectedCommand = new RelayCommand(CleanSelectedTargets, () => !IsCleanupBusy && _cleanableCategories.Any(category => category.IsSelected && category.CanClean));
        _generateCleanupReportCommand = new RelayCommand(GenerateCleanupReport, () => _cleanableCategories.Count > 0);
        _exportHtmlReportCommand = new RelayCommand(ExportHtmlReport, () => _programs.Count > 0);
        _exportCsvReportCommand = new RelayCommand(ExportCsvReport, () => _programs.Count > 0);
        _exportJsonReportCommand = new RelayCommand(ExportJsonReport, () => _programs.Count > 0);
        RefreshCommand = _refreshInventoryCommand;
        RefreshUsageCommand = _refreshUsageCommand;
        ScanCleanupCommand = _scanCleanupCommand;
        PreviewCleanupCommand = _previewCleanupCommand;
        CleanSelectedCommand = _cleanSelectedCommand;
        GenerateCleanupReportCommand = _generateCleanupReportCommand;
        ExportHtmlReportCommand = _exportHtmlReportCommand;
        ExportCsvReportCommand = _exportCsvReportCommand;
        ExportJsonReportCommand = _exportJsonReportCommand;

        _programUsageTracker.UsageUpdated += OnUsageUpdated;

        LoadUsageRecords();
        LoadPrograms();
    }

    public ICollectionView ProgramsView { get; }

    public ICollectionView UsageView { get; }

    public ICollectionView CleanupView { get; }

    public ICommand RefreshCommand { get; }

    public ICommand RefreshUsageCommand { get; }

    public ICommand ScanCleanupCommand { get; }

    public ICommand PreviewCleanupCommand { get; }

    public ICommand CleanSelectedCommand { get; }

    public ICommand GenerateCleanupReportCommand { get; }

    public ICommand ExportHtmlReportCommand { get; }

    public ICommand ExportCsvReportCommand { get; }

    public ICommand ExportJsonReportCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ProgramsView.Refresh();
                UpdateInventoryStatusText();
            }
        }
    }

    public string InventoryStatusText
    {
        get => _inventoryStatusText;
        private set => SetProperty(ref _inventoryStatusText, value);
    }

    public string UsageStatusText
    {
        get => _usageStatusText;
        private set => SetProperty(ref _usageStatusText, value);
    }

    public string CleanupStatusText
    {
        get => _cleanupStatusText;
        private set => SetProperty(ref _cleanupStatusText, value);
    }

    public string ReportStatusText
    {
        get => _reportStatusText;
        private set => SetProperty(ref _reportStatusText, value);
    }

    public bool IsInventoryLoading
    {
        get => _isInventoryLoading;
        private set
        {
            if (SetProperty(ref _isInventoryLoading, value))
            {
                _refreshInventoryCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsUsageLoading
    {
        get => _isUsageLoading;
        private set
        {
            if (SetProperty(ref _isUsageLoading, value))
            {
                _refreshUsageCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsCleanupBusy
    {
        get => _isCleanupBusy;
        private set
        {
            if (SetProperty(ref _isCleanupBusy, value))
            {
                _scanCleanupCommand.RaiseCanExecuteChanged();
                _previewCleanupCommand.RaiseCanExecuteChanged();
                _cleanSelectedCommand.RaiseCanExecuteChanged();
                _generateCleanupReportCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private void LoadPrograms()
    {
        IsInventoryLoading = true;

        try
        {
            var programs = _installedProgramService.GetInstalledPrograms();
            var usageRecords = _usageRepository.GetUsageRecords();

            _programs.Clear();
            foreach (var program in programs)
            {
                var match = _programProcessMatcher.Match(program, usageRecords);
                var recommendation = _cleanupRecommendationEngine.Evaluate(program, match);
                _programs.Add(new InstalledProgramViewModel
                {
                    Program = program,
                    MatchedProcessName = match.MatchedProcessName,
                    MatchConfidence = match.ConfidenceScore,
                    LastUsedUtc = match.LastSeenUtc,
                    TotalObservedMinutes = match.TotalObservedMinutes,
                    LaunchCount = match.LaunchCount,
                    UsageScore = recommendation.UsageScore,
                    SizeScore = recommendation.SizeScore,
                    SystemRiskScore = recommendation.SystemRiskScore,
                    DependencyRiskScore = recommendation.DependencyRiskScore,
                    FinalCleanupScore = recommendation.FinalCleanupScore,
                    Recommendation = recommendation.Recommendation,
                    RecommendationReasons = recommendation.Reasons,
                });
            }

            ProgramsView.Refresh();
            UpdateInventoryStatusText();
            UpdateReportStatusText();
        }
        finally
        {
            IsInventoryLoading = false;
        }
    }

    private void LoadUsageRecords()
    {
        IsUsageLoading = true;

        try
        {
            var usageRecords = _usageRepository.GetUsageRecords();

            _usageRecords.Clear();
            foreach (var usageRecord in usageRecords)
            {
                _usageRecords.Add(usageRecord);
            }

            UsageView.Refresh();
            UpdateUsageStatusText();
        }
        finally
        {
            IsUsageLoading = false;
        }
    }

    private void ScanCleanupTargets()
    {
        IsCleanupBusy = true;

        try
        {
            var categories = _safeDiskCleanupService.Scan();
            _cleanableCategories.Clear();

            foreach (var category in categories)
            {
                var vm = new CleanableCategoryViewModel
                {
                    Category = category,
                    IsSelected = category.CanClean && category.TotalSizeMB > 0,
                };
                vm.PropertyChanged += OnCleanupSelectionChanged;
                _cleanableCategories.Add(vm);
            }

            CleanupView.Refresh();
            UpdateCleanupStatusText();
            UpdateReportStatusText();
        }
        finally
        {
            IsCleanupBusy = false;
        }
    }

    private void PreviewCleanupTargets()
    {
        var categories = _cleanableCategories.ToList();
        if (categories.Count == 0)
        {
            MessageBox.Show("No cleanup scan data is available yet.", "Preview", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        MessageBox.Show(BuildCleanupSummary(categories), "Cleanup Preview", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CleanSelectedTargets()
    {
        var selected = _cleanableCategories
            .Where(category => category.IsSelected && category.CanClean)
            .ToList();

        if (selected.Count == 0)
        {
            MessageBox.Show("Select at least one cleanable category first.", "Clean Selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var totalSizeMb = selected.Sum(category => category.TotalSizeMB);
        var confirmationMessage =
            "The following categories will be cleaned:\n\n" +
            string.Join(Environment.NewLine, selected.Select(category => $"- {category.CategoryName} ({category.TotalSizeMB:N1} MB)")) +
            $"\n\nTotal targeted size: {totalSizeMb:N1} MB\n\n" +
            "Files will only be removed from the safe cleanup categories. Continue?";

        var confirmation = MessageBox.Show(
            confirmationMessage,
            "Confirm Cleanup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        IsCleanupBusy = true;

        try
        {
            var result = _safeDiskCleanupService.Clean(selected.Select(category => category.Category).ToList());
            _cleanupLogService.Log($"Cleanup confirmed by user. {result.Summary}");
            ScanCleanupTargets();
            MessageBox.Show(result.Summary, "Cleanup Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _cleanupLogService.Log($"Cleanup failed: {ex.Message}");
            MessageBox.Show($"Cleanup failed: {ex.Message}", "Cleanup Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsCleanupBusy = false;
        }
    }

    private void GenerateCleanupReport()
    {
        var categories = _cleanableCategories.ToList();
        if (categories.Count == 0)
        {
            MessageBox.Show("Run a cleanup scan first.", "Generate Report", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var reportPath = _cleanupLogService.GetLogFilePath().Replace(".log", "-report.txt", StringComparison.OrdinalIgnoreCase);
        File.WriteAllText(reportPath, BuildCleanupSummary(categories));
        _cleanupLogService.Log($"Cleanup report generated at '{reportPath}'.");
        CleanupStatusText = $"Cleanup report generated: {reportPath}";
        MessageBox.Show($"Report created at:\n{reportPath}", "Report Generated", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExportHtmlReport()
    {
        ExportReport(snapshot => _reportGeneratorService.ExportHtml(snapshot), "HTML");
    }

    private void ExportCsvReport()
    {
        ExportReport(snapshot => _reportGeneratorService.ExportCsv(snapshot), "CSV");
    }

    private void ExportJsonReport()
    {
        ExportReport(snapshot => _reportGeneratorService.ExportJson(snapshot), "JSON");
    }

    private void ExportReport(Func<ReportSnapshot, string> exportFunc, string formatName)
    {
        var snapshot = BuildReportSnapshot();
        var filePath = exportFunc(snapshot);
        _cleanupLogService.Log($"{formatName} report exported to '{filePath}'.");
        ReportStatusText = $"{formatName} report exported: {filePath}";
        MessageBox.Show($"{formatName} report created at:\n{filePath}", "Report Exported", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private ReportSnapshot BuildReportSnapshot()
    {
        var generatedAtUtc = DateTime.UtcNow;
        var reportPrograms = _programs
            .Select(program => new ReportProgramItem
            {
                DisplayName = program.DisplayName,
                Publisher = program.Publisher,
                DisplayVersion = program.DisplayVersion,
                EstimatedSizeMB = program.EstimatedSizeMB,
                InstallDate = program.InstallDate,
                LastUsedUtc = program.LastUsedUtc,
                MatchedProcessName = program.MatchedProcessName,
                MatchConfidence = program.MatchConfidence,
                UsageScore = program.UsageScore,
                SizeScore = program.SizeScore,
                SystemRiskScore = program.SystemRiskScore,
                DependencyRiskScore = program.DependencyRiskScore,
                FinalCleanupScore = program.FinalCleanupScore,
                Recommendation = program.Recommendation,
                RecommendationReasons = program.RecommendationReasons,
            })
            .ToList();

        var cleanupItems = _cleanableCategories
            .Select(category => new ReportCleanupItem
            {
                CategoryName = category.CategoryName,
                FileCount = category.FileCount,
                TotalSizeMB = category.TotalSizeMB,
                RiskLevel = category.RiskLevel,
                Description = category.Description,
                CanClean = category.CanClean,
            })
            .ToList();

        var unusedPrograms = reportPrograms.Count(program => program.LastUsedUtc is null || (generatedAtUtc - program.LastUsedUtc.Value).TotalDays > 180);
        var cleanupCandidates = reportPrograms.Count(program => string.Equals(program.Recommendation, "CleanupCandidate", StringComparison.OrdinalIgnoreCase));
        var dependencySensitive = reportPrograms.Count(program => program.DependencyRiskScore > 0 || string.Equals(program.Recommendation, "DoNotRemove", StringComparison.OrdinalIgnoreCase));
        var temporaryCleanupSize = cleanupItems.Sum(item => item.TotalSizeMB);
        var estimatedReclaimable = temporaryCleanupSize + reportPrograms
            .Where(program => string.Equals(program.Recommendation, "CleanupCandidate", StringComparison.OrdinalIgnoreCase))
            .Sum(program => program.EstimatedSizeMB ?? 0);

        return new ReportSnapshot
        {
            GeneratedAtUtc = generatedAtUtc,
            Summary = new ReportSummary
            {
                TotalInstalledPrograms = reportPrograms.Count,
                UnusedSixMonthsCount = unusedPrograms,
                CleanupCandidateCount = cleanupCandidates,
                DependencySensitiveCount = dependencySensitive,
                TemporaryCleanupSizeMB = temporaryCleanupSize,
                EstimatedReclaimableSizeMB = estimatedReclaimable,
            },
            Programs = reportPrograms,
            CleanupPreview = cleanupItems,
        };
    }

    private bool FilterProgram(object item)
    {
        if (item is not InstalledProgramViewModel program)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        return program.DisplayName.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) ||
               program.Publisher.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) ||
               program.DisplayVersion.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) ||
               program.MatchedProcessName.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) ||
               program.Recommendation.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) ||
               program.RecommendationReasons.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase);
    }

    private void UpdateInventoryStatusText()
    {
        var visibleCount = ProgramsView.Cast<object>().Count();
        var matchedCount = _programs.Count(program => program.MatchConfidence > 0);
        var cleanupCandidates = _programs.Count(program => program.Recommendation == "CleanupCandidate");
        InventoryStatusText = $"{visibleCount} program listed. {matchedCount} matched to observed processes. {cleanupCandidates} cleanup candidates.";
    }

    private void UpdateUsageStatusText()
    {
        var visibleCount = UsageView.Cast<object>().Count();
        UsageStatusText = $"{visibleCount} usage records stored. Tracking interval: 30 seconds.";
    }

    private void UpdateCleanupStatusText()
    {
        var categories = _cleanableCategories.Count;
        var selected = _cleanableCategories.Count(category => category.IsSelected && category.CanClean);
        var totalSize = _cleanableCategories.Sum(category => category.TotalSizeMB);
        CleanupStatusText = $"{categories} cleanup categories scanned. {selected} selected. Approx. {totalSize:N1} MB visible. Log: {_cleanupLogService.GetLogFilePath()}";
        _cleanSelectedCommand.RaiseCanExecuteChanged();
        _previewCleanupCommand.RaiseCanExecuteChanged();
        _generateCleanupReportCommand.RaiseCanExecuteChanged();
    }

    private void UpdateReportStatusText()
    {
        var totalPrograms = _programs.Count;
        var unusedCount = _programs.Count(program => program.LastUsedUtc is null || (DateTime.UtcNow - program.LastUsedUtc.Value).TotalDays > 180);
        var largestProgramName = _programs
            .OrderByDescending(program => program.EstimatedSizeMB ?? 0)
            .FirstOrDefault()?.DisplayName ?? "n/a";
        ReportStatusText = $"{totalPrograms} programs ready for export. Unused 6+ months: {unusedCount}. Largest tracked program: {largestProgramName}.";
        _exportHtmlReportCommand.RaiseCanExecuteChanged();
        _exportCsvReportCommand.RaiseCanExecuteChanged();
        _exportJsonReportCommand.RaiseCanExecuteChanged();
    }

    private void OnUsageUpdated(object? sender, EventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LoadUsageRecords();
            LoadPrograms();
        });
    }

    private void OnCleanupSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CleanableCategoryViewModel.IsSelected))
        {
            UpdateCleanupStatusText();
        }
    }

    private static string BuildCleanupSummary(IReadOnlyList<CleanableCategoryViewModel> categories)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Safe Disk Cleanup Summary");
        builder.AppendLine();

        foreach (var category in categories)
        {
            builder.AppendLine($"{category.CategoryName}");
            builder.AppendLine($"  Files: {category.FileCount}");
            builder.AppendLine($"  Size: {category.TotalSizeMB.ToString("N1", CultureInfo.InvariantCulture)} MB");
            builder.AppendLine($"  Risk: {category.RiskLevel}");
            builder.AppendLine($"  Cleanable: {(category.CanClean ? "Yes" : "No")}");
            builder.AppendLine($"  Description: {category.Description}");
            builder.AppendLine();
        }

        builder.AppendLine($"Total Size: {categories.Sum(category => category.TotalSizeMB).ToString("N1", CultureInfo.InvariantCulture)} MB");
        return builder.ToString();
    }
}
