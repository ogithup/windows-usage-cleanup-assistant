using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WindowsUsageCleanupAssistant.Commands;
using WindowsUsageCleanupAssistant.Models;
using WindowsUsageCleanupAssistant.Services;
using WindowsUsageCleanupAssistant.Views;

namespace WindowsUsageCleanupAssistant.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IInstalledProgramService _installedProgramService;
    private readonly IUsageRepository _usageRepository;
    private readonly IProgramUsageTracker _programUsageTracker;
    private readonly IProgramProcessMatcher _programProcessMatcher;
    private readonly ICleanupRecommendationEngine _cleanupRecommendationEngine;
    private readonly ILlmExplanationService _llmExplanationService;
    private readonly ISafeDiskCleanupService _safeDiskCleanupService;
    private readonly ICleanupLogService _cleanupLogService;
    private readonly IReportGeneratorService _reportGeneratorService;
    private readonly RelayCommand _refreshInventoryCommand;
    private readonly RelayCommand _refreshUsageCommand;
    private readonly RelayCommand _scanCleanupCommand;
    private readonly RelayCommand _previewCleanupCommand;
    private readonly RelayCommand _cleanSelectedCommand;
    private readonly RelayCommand _generateCleanupReportCommand;
    private readonly RelayCommand _exportHtmlReportCommand;
    private readonly RelayCommand _exportCsvReportCommand;
    private readonly RelayCommand _exportJsonReportCommand;
    private readonly RelayCommand _uninstallSelectedCommand;
    private readonly RelayCommand _openCleanupLogCommand;
    private NavigationItemViewModel? _selectedNavigationItem;
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
        ILlmExplanationService llmExplanationService,
        ISafeDiskCleanupService safeDiskCleanupService,
        ICleanupLogService cleanupLogService,
        IReportGeneratorService reportGeneratorService)
    {
        _installedProgramService = installedProgramService;
        _usageRepository = usageRepository;
        _programUsageTracker = programUsageTracker;
        _programProcessMatcher = programProcessMatcher;
        _cleanupRecommendationEngine = cleanupRecommendationEngine;
        _llmExplanationService = llmExplanationService;
        _safeDiskCleanupService = safeDiskCleanupService;
        _cleanupLogService = cleanupLogService;
        _reportGeneratorService = reportGeneratorService;

        InstalledPrograms = [];
        UsageRecords = [];
        CleanableCategories = [];

        Dashboard = new DashboardViewModel(this);
        InstalledApps = new InstalledAppsViewModel(this);
        Usage = new UsageViewModel(this);
        Cleanup = new CleanupViewModel(this);
        Reports = new ReportsViewModel(this);
        Settings = new SettingsViewModel(this);

        NavigationItems =
        [
            new NavigationItemViewModel { Title = "Dashboard", Subtitle = "Overview", Page = Dashboard, IsSelected = true },
            new NavigationItemViewModel { Title = "Installed Apps", Subtitle = "Inventory", Page = InstalledApps },
            new NavigationItemViewModel { Title = "Usage Tracking", Subtitle = "Observed activity", Page = Usage },
            new NavigationItemViewModel { Title = "Cleanup", Subtitle = "Safe cleanup", Page = Cleanup },
            new NavigationItemViewModel { Title = "Reports", Subtitle = "Exports", Page = Reports },
            new NavigationItemViewModel { Title = "Settings", Subtitle = "Providers and paths", Page = Settings },
        ];
        _selectedNavigationItem = NavigationItems[0];

        _refreshInventoryCommand = new RelayCommand(LoadPrograms, () => !IsInventoryLoading);
        _refreshUsageCommand = new RelayCommand(LoadUsageRecords, () => !IsUsageLoading);
        _scanCleanupCommand = new RelayCommand(ScanCleanupTargets, () => !IsCleanupBusy);
        _previewCleanupCommand = new RelayCommand(PreviewCleanupTargets, () => !IsCleanupBusy && CleanableCategories.Count > 0);
        _cleanSelectedCommand = new RelayCommand(CleanSelectedTargets, () => !IsCleanupBusy && CleanableCategories.Any(category => category.IsSelected && category.CanClean));
        _generateCleanupReportCommand = new RelayCommand(GenerateCleanupReport, () => CleanableCategories.Count > 0);
        _exportHtmlReportCommand = new RelayCommand(ExportHtmlReport, () => InstalledPrograms.Count > 0);
        _exportCsvReportCommand = new RelayCommand(ExportCsvReport, () => InstalledPrograms.Count > 0);
        _exportJsonReportCommand = new RelayCommand(ExportJsonReport, () => InstalledPrograms.Count > 0);
        _uninstallSelectedCommand = new RelayCommand(SafeUninstallSelectedProgram, () => InstalledApps.SelectedProgram is not null);
        _openCleanupLogCommand = new RelayCommand(OpenCleanupLog, () => File.Exists(CleanupLogPath));

        _programUsageTracker.UsageUpdated += OnUsageUpdated;

        LoadUsageRecords();
        LoadPrograms();
    }

    public ObservableCollection<InstalledProgramViewModel> InstalledPrograms { get; }

    public ObservableCollection<UsageRecord> UsageRecords { get; }

    public ObservableCollection<CleanableCategoryViewModel> CleanableCategories { get; }

    public DashboardViewModel Dashboard { get; }

    public InstalledAppsViewModel InstalledApps { get; }

    public UsageViewModel Usage { get; }

    public CleanupViewModel Cleanup { get; }

    public ReportsViewModel Reports { get; }

    public SettingsViewModel Settings { get; }

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }

    public NavigationItemViewModel? SelectedNavigationItem
    {
        get => _selectedNavigationItem;
        set
        {
            if (SetProperty(ref _selectedNavigationItem, value))
            {
                foreach (var item in NavigationItems)
                {
                    item.IsSelected = item == value;
                }

                OnPropertyChanged(nameof(CurrentPage));
                OnPropertyChanged(nameof(CurrentSectionTitle));
                OnPropertyChanged(nameof(CurrentSectionSubtitle));
            }
        }
    }

    public ViewModelBase CurrentPage => SelectedNavigationItem?.Page ?? Dashboard;

    public string CurrentSectionTitle => SelectedNavigationItem?.Title ?? "Dashboard";

    public string CurrentSectionSubtitle => SelectedNavigationItem?.Subtitle ?? "Overview";

    public ICommand RefreshCommand => _refreshInventoryCommand;

    public ICommand RefreshUsageCommand => _refreshUsageCommand;

    public ICommand ScanCleanupCommand => _scanCleanupCommand;

    public ICommand PreviewCleanupCommand => _previewCleanupCommand;

    public ICommand CleanSelectedCommand => _cleanSelectedCommand;

    public ICommand GenerateCleanupReportCommand => _generateCleanupReportCommand;

    public ICommand ExportHtmlReportCommand => _exportHtmlReportCommand;

    public ICommand ExportCsvReportCommand => _exportCsvReportCommand;

    public ICommand ExportJsonReportCommand => _exportJsonReportCommand;

    public ICommand UninstallSelectedCommand => _uninstallSelectedCommand;

    public ICommand OpenCleanupLogCommand => _openCleanupLogCommand;

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

    public string DatabasePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowsUsageCleanupAssistant",
        "usage-tracking.db");

    public string CleanupLogPath => _cleanupLogService.GetLogFilePath();

    public string ReportsDirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowsUsageCleanupAssistant",
        "Reports");

    private void LoadPrograms()
    {
        IsInventoryLoading = true;

        try
        {
            var programs = _installedProgramService.GetInstalledPrograms();
            var usageRecords = _usageRepository.GetUsageRecords();

            InstalledPrograms.Clear();
            foreach (var program in programs)
            {
                var match = _programProcessMatcher.Match(program, usageRecords);
                var recommendation = _cleanupRecommendationEngine.Evaluate(program, match);
                var analysis = BuildProgramAnalysis(program, match, recommendation);
                var llmExplanation = SafeGenerateExplanation(analysis);

                InstalledPrograms.Add(new InstalledProgramViewModel
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
                    LlmExplanation = llmExplanation,
                    CategoryLabel = analysis.Category,
                    RiskFlagsLabel = string.Join(", ", analysis.RiskFlags),
                });
            }

            UpdateInventoryStatusText();
            InstalledApps.Refresh();
            Dashboard.Refresh();
            Reports.Refresh();
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

            UsageRecords.Clear();
            foreach (var usageRecord in usageRecords)
            {
                UsageRecords.Add(usageRecord);
            }

            UpdateUsageStatusText();
            Usage.Refresh();
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
            CleanableCategories.Clear();

            foreach (var category in categories)
            {
                var vm = new CleanableCategoryViewModel
                {
                    Category = category,
                    IsSelected = category.CanClean && category.TotalSizeMB > 0,
                };
                vm.PropertyChanged += OnCleanupSelectionChanged;
                CleanableCategories.Add(vm);
            }

            UpdateCleanupStatusText();
            Cleanup.Refresh();
            Dashboard.Refresh();
            Reports.Refresh();
        }
        finally
        {
            IsCleanupBusy = false;
        }
    }

    private void PreviewCleanupTargets()
    {
        var categories = CleanableCategories.ToList();
        if (categories.Count == 0)
        {
            MessageBox.Show("No cleanup scan data is available yet.", "Preview", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        MessageBox.Show(BuildCleanupSummary(categories), "Cleanup Preview", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void CleanSelectedTargets()
    {
        var selected = CleanableCategories
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
        var progressViewModel = new CleanupProgressViewModel();
        var progressWindow = new CleanupProgressWindow(progressViewModel);
        if (Application.Current.MainWindow is Window mainWindow)
        {
            progressWindow.Owner = mainWindow;
        }

        try
        {
            progressWindow.Show();
            var progress = new Progress<CleanupProgressUpdate>(progressViewModel.Apply);
            var result = await Task.Run(() => _safeDiskCleanupService.Clean(
                selected.Select(category => category.Category).ToList(),
                Cleanup.SkipLockedFilesAutomatically,
                progress));
            _cleanupLogService.Log($"Cleanup confirmed by user. {result.Summary}");
            Cleanup.LastSkippedEntries = result.SkippedEntries;
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
            progressWindow.Close();
            IsCleanupBusy = false;
        }
    }

    private void GenerateCleanupReport()
    {
        var categories = CleanableCategories.ToList();
        if (categories.Count == 0)
        {
            MessageBox.Show("Run a cleanup scan first.", "Generate Report", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var reportPath = _cleanupLogService.GetLogFilePath().Replace(".log", "-report.txt", StringComparison.OrdinalIgnoreCase);
        File.WriteAllText(reportPath, BuildCleanupSummary(categories));
        _cleanupLogService.Log($"Cleanup report generated at '{reportPath}'.");
        CleanupStatusText = $"Cleanup report generated: {reportPath}";
        Cleanup.Refresh();
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
        Reports.Refresh();
        MessageBox.Show($"{formatName} report created at:\n{filePath}", "Report Exported", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SafeUninstallSelectedProgram()
    {
        var selected = InstalledApps.SelectedProgram;
        if (selected is null)
        {
            MessageBox.Show("Select an installed program first.", "Safe Uninstall", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var details = BuildUninstallDetails(selected);
        var confirmation = MessageBox.Show(
            details + "\n\nThis will open the program's Windows uninstall command. The app will not uninstall silently. Continue?",
            "Confirm Safe Uninstall",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(selected.UninstallString))
        {
            _cleanupLogService.Log($"Uninstall attempt blocked for '{selected.DisplayName}': missing UninstallString.");
            MessageBox.Show("No uninstall command was found for this program. Please use Windows Settings or Control Panel to review it manually.", "Uninstall Not Available", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var process = StartUninstallProcess(selected.UninstallString);
            _cleanupLogService.Log($"Uninstall attempt started for '{selected.DisplayName}' using '{selected.UninstallString}'.");

            if (process is not null)
            {
                try
                {
                    process.EnableRaisingEvents = true;
                    process.Exited += (_, _) =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _cleanupLogService.Log($"Uninstall process exited for '{selected.DisplayName}'. Triggering rescan.");
                            LoadPrograms();
                        });
                        process.Dispose();
                    };
                }
                catch
                {
                }
            }

            MessageBox.Show("The Windows uninstall flow has been opened. After the uninstall finishes, the app will try to rescan automatically. You can also click Refresh manually.", "Uninstall Started", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _cleanupLogService.Log($"Uninstall attempt failed for '{selected.DisplayName}': {ex.Message}");
            MessageBox.Show($"Unable to start uninstall command.\n\n{ex.Message}", "Uninstall Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private ReportSnapshot BuildReportSnapshot()
    {
        var generatedAtUtc = DateTime.UtcNow;
        var reportPrograms = InstalledPrograms
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
                LlmExplanation = program.LlmExplanation,
            })
            .ToList();

        var cleanupItems = CleanableCategories
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

    private void UpdateInventoryStatusText()
    {
        var matchedCount = InstalledPrograms.Count(program => program.MatchConfidence > 0);
        var cleanupCandidates = InstalledPrograms.Count(program => program.Recommendation == "CleanupCandidate");
        InventoryStatusText = $"{InstalledPrograms.Count} program listed. {matchedCount} matched to observed processes. {cleanupCandidates} cleanup candidates.";
        _uninstallSelectedCommand.RaiseCanExecuteChanged();
    }

    private void UpdateUsageStatusText()
    {
        UsageStatusText = $"{UsageRecords.Count} usage records stored. Tracking interval: 30 seconds.";
    }

    private void UpdateCleanupStatusText()
    {
        var selected = CleanableCategories.Count(category => category.IsSelected && category.CanClean);
        var totalSize = CleanableCategories.Sum(category => category.TotalSizeMB);
        CleanupStatusText = $"{CleanableCategories.Count} cleanup categories scanned. {selected} selected. Approx. {totalSize:N1} MB visible. Log: {_cleanupLogService.GetLogFilePath()}";
        _cleanSelectedCommand.RaiseCanExecuteChanged();
        _previewCleanupCommand.RaiseCanExecuteChanged();
        _generateCleanupReportCommand.RaiseCanExecuteChanged();
        _openCleanupLogCommand.RaiseCanExecuteChanged();
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
            Cleanup.Refresh();
            Dashboard.Refresh();
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

    private string SafeGenerateExplanation(ProgramAnalysisDto analysis)
    {
        try
        {
            return _llmExplanationService.GenerateExplanationAsync(analysis).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _cleanupLogService.Log($"LLM explanation generation failed for '{analysis.ProgramName}': {ex.Message}");
            return "Açıklama üretilemedi. Programı kaldırmadan önce kullanım durumu ve bağımlılık risklerini manuel olarak kontrol edin.";
        }
    }

    private static ProgramAnalysisDto BuildProgramAnalysis(
        InstalledProgram program,
        ProgramProcessMatch match,
        CleanupRecommendation recommendation)
    {
        return new ProgramAnalysisDto
        {
            ProgramName = program.DisplayName,
            Publisher = program.Publisher,
            SizeMB = program.EstimatedSizeMB,
            LastUsedUtc = match.LastSeenUtc,
            LastUsedLabel = FormatLastUsedLabel(match.LastSeenUtc),
            Category = InferCategory(program),
            RiskFlags = BuildRiskFlags(program, recommendation),
            Recommendation = recommendation.Recommendation,
            FinalCleanupScore = recommendation.FinalCleanupScore,
            RecommendationReasons = recommendation.Reasons,
        };
    }

    private static string FormatLastUsedLabel(DateTime? lastUsedUtc)
    {
        if (lastUsedUtc is null)
        {
            return "kullanım verisi yok";
        }

        var days = (DateTime.UtcNow - lastUsedUtc.Value).TotalDays;
        if (days < 1)
        {
            return "bugün";
        }

        if (days < 30)
        {
            return $"{Math.Floor(days).ToString(CultureInfo.InvariantCulture)} gün önce";
        }

        var months = Math.Floor(days / 30);
        if (months < 12)
        {
            return $"{months.ToString(CultureInfo.InvariantCulture)} ay önce";
        }

        var years = Math.Floor(days / 365);
        return $"{years.ToString(CultureInfo.InvariantCulture)} yıl önce";
    }

    private static string InferCategory(InstalledProgram program)
    {
        var name = program.DisplayName;
        var publisher = program.Publisher;

        if (ContainsAny(name, "SDK", "Runtime", "Redistributable", "Framework", "JDK", ".NET"))
        {
            return "SDK / Runtime";
        }

        if (ContainsAny(name, "Game", "Launcher", "Steam", "Epic", "Ubisoft", "Battle.net"))
        {
            return "Game";
        }

        if (ContainsAny(name, "Studio", "Code", "IDE", "Unity", "Visual Studio", "Android") ||
            ContainsAny(publisher, "JetBrains", "Eclipse Foundation"))
        {
            return "Development Tool";
        }

        if (ContainsAny(publisher, "Microsoft", "Intel", "NVIDIA", "AMD", "Realtek"))
        {
            return "System Component";
        }

        return "Application";
    }

    private static IReadOnlyList<string> BuildRiskFlags(InstalledProgram program, CleanupRecommendation recommendation)
    {
        var flags = new List<string>();
        var name = program.DisplayName;
        var publisher = program.Publisher;

        if (ContainsAny(name, "SDK", "Runtime", "Redistributable", "Framework", "JDK", ".NET"))
        {
            flags.Add("SDK");
        }

        if (ContainsAny(name, "Driver") || ContainsAny(publisher, "Intel", "NVIDIA", "AMD", "Realtek"))
        {
            flags.Add("Sistem Bileşeni");
        }

        if (ContainsAny(name, "Studio", "Code", "IDE", "Unity", "Android"))
        {
            flags.Add("Geliştirici Aracı");
        }

        if (recommendation.DependencyRiskScore > 0)
        {
            flags.Add("Bağımlılık Riski");
        }

        if (recommendation.SystemRiskScore > 0)
        {
            flags.Add("Yayıncı Riski");
        }

        return flags.Distinct(StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static bool ContainsAny(string value, params string[] candidates)
    {
        return candidates.Any(candidate => value.Contains(candidate, StringComparison.CurrentCultureIgnoreCase));
    }

    public void NotifyInstalledProgramSelectionChanged()
    {
        _uninstallSelectedCommand.RaiseCanExecuteChanged();
    }

    private static string BuildUninstallDetails(InstalledProgramViewModel program)
    {
        var sizeLabel = program.EstimatedSizeMB is > 0 ? $"{program.EstimatedSizeMB} MB" : "Unknown";
        var lastUsedLabel = program.LastUsedUtc?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "No usage data";
        var riskFlags = string.IsNullOrWhiteSpace(program.RiskFlagsLabel) ? "None" : program.RiskFlagsLabel;

        return
            $"Program: {program.DisplayName}\n" +
            $"Publisher: {program.Publisher}\n" +
            $"Size: {sizeLabel}\n" +
            $"Last used: {lastUsedLabel}\n" +
            $"Risk flags: {riskFlags}\n" +
            $"Recommendation: {program.Recommendation}\n" +
            $"Reasons: {program.RecommendationReasons}";
    }

    private static Process? StartUninstallProcess(string uninstallString)
    {
        var trimmed = uninstallString.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        if (trimmed.StartsWith("msiexec", StringComparison.OrdinalIgnoreCase))
        {
            return Process.Start(new ProcessStartInfo
            {
                FileName = "msiexec.exe",
                Arguments = trimmed.Length > 7 ? trimmed[7..].Trim() : string.Empty,
                UseShellExecute = true,
            });
        }

        var (fileName, arguments) = SplitCommand(trimmed);
        return Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = true,
        });
    }

    private static (string FileName, string Arguments) SplitCommand(string command)
    {
        if (command.StartsWith('"'))
        {
            var closingQuote = command.IndexOf('"', 1);
            if (closingQuote > 1)
            {
                var fileName = command[1..closingQuote];
                var arguments = command[(closingQuote + 1)..].Trim();
                return (fileName, arguments);
            }
        }

        var firstSpace = command.IndexOf(' ');
        return firstSpace > 0
            ? (command[..firstSpace], command[(firstSpace + 1)..].Trim())
            : (command, string.Empty);
    }

    private void OpenCleanupLog()
    {
        try
        {
            if (!File.Exists(CleanupLogPath))
            {
                MessageBox.Show("Cleanup log file was not found yet.", "Open Log", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = CleanupLogPath,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open cleanup log.\n\n{ex.Message}", "Open Log Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
