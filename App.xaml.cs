using System.IO;
using System.Windows;
using WindowsUsageCleanupAssistant.Services;
using WindowsUsageCleanupAssistant.ViewModels;

namespace WindowsUsageCleanupAssistant;

public partial class App : Application
{
    private IProgramUsageTracker? _programUsageTracker;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var installedProgramService = new InstalledProgramService();
        var databasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsUsageCleanupAssistant",
            "usage-tracking.db");
        var usageRepository = new SqliteUsageRepository(databasePath);
        _programUsageTracker = new ProgramUsageTrackerService(usageRepository);
        var programProcessMatcher = new ProgramProcessMatcherService();
        var cleanupRecommendationEngine = new CleanupRecommendationEngine();
        var llmExplanationService = new MockLlmExplanationService();
        var cleanupLogService = new FileCleanupLogService(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsUsageCleanupAssistant",
            "cleanup.log"));
        var safeDiskCleanupService = new SafeDiskCleanupService(cleanupLogService);
        var reportGeneratorService = new ReportGeneratorService(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsUsageCleanupAssistant",
            "Reports"));
        _programUsageTracker.Start();

        var mainViewModel = new MainViewModel(
            installedProgramService,
            usageRepository,
            _programUsageTracker,
            programProcessMatcher,
            cleanupRecommendationEngine,
            llmExplanationService,
            safeDiskCleanupService,
            cleanupLogService,
            reportGeneratorService);
        var mainWindow = new MainWindow(mainViewModel);
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_programUsageTracker is not null)
        {
            _programUsageTracker.StopAsync().GetAwaiter().GetResult();
        }

        base.OnExit(e);
    }
}
