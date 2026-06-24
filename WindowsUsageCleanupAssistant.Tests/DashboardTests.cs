using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WindowsUsageCleanupAssistant.Models;
using WindowsUsageCleanupAssistant.Services;
using WindowsUsageCleanupAssistant.ViewModels;
using Xunit;

namespace WindowsUsageCleanupAssistant.Tests;

public class DashboardTests
{
    private class MockInstalledProgramService : IInstalledProgramService
    {
        public List<InstalledProgram> Programs { get; } = new();
        public IReadOnlyList<InstalledProgram> GetInstalledPrograms() => Programs;
    }

    private class MockUsageRepository : IUsageRepository
    {
        public List<UsageRecord> Records { get; } = new();
        public void Initialize() { }
        public void RecordObservation(string processName, string executablePath, DateTime observedAtUtc, double observedMinutes, bool incrementLaunchCount) { }
        public IReadOnlyList<UsageRecord> GetUsageRecords() => Records;
    }

    private class MockProgramUsageTracker : IProgramUsageTracker
    {
        public event EventHandler? UsageUpdated;
        public void Start() { }
        public Task StopAsync() => Task.CompletedTask;
        public void FireUsageUpdated() => UsageUpdated?.Invoke(this, EventArgs.Empty);
    }

    private class MockProgramProcessMatcher : IProgramProcessMatcher
    {
        public Dictionary<string, ProgramProcessMatch> Matches { get; } = new();
        public ProgramProcessMatch Match(InstalledProgram program, IReadOnlyList<UsageRecord> usageRecords)
        {
            if (Matches.TryGetValue(program.DisplayName, out var match))
            {
                return match;
            }
            return new ProgramProcessMatch { MatchedProcessName = program.DisplayName + ".exe" };
        }
    }

    private class MockCleanupRecommendationEngine : ICleanupRecommendationEngine
    {
        public Dictionary<string, CleanupRecommendation> Recommendations { get; } = new();
        public CleanupRecommendation Evaluate(InstalledProgram program, ProgramProcessMatch match)
        {
            if (Recommendations.TryGetValue(program.DisplayName, out var rec))
            {
                return rec;
            }
            return new CleanupRecommendation { Recommendation = "Review" };
        }
    }

    private class MockLlmExplanationService : ILlmExplanationService
    {
        public Task<string> GenerateExplanationAsync(ProgramAnalysisDto analysis, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("Mock LLM Explanation for " + analysis.ProgramName);
        }
    }

    private class MockSafeDiskCleanupService : ISafeDiskCleanupService
    {
        public List<CleanableCategory> Categories { get; } = new();
        public IReadOnlyList<CleanableCategory> Scan() => Categories;
        public CleanupExecutionResult Clean(IReadOnlyList<CleanableCategory> categories, bool skipLockedFilesAutomatically, IProgress<CleanupProgressUpdate>? progress = null)
        {
            return new CleanupExecutionResult { Summary = "Cleaned" };
        }
    }

    private class MockCleanupLogService : ICleanupLogService
    {
        public void Log(string message) { }
        public string GetLogFilePath() => "mock.log";
    }

    private class MockReportGeneratorService : IReportGeneratorService
    {
        public string ExportHtml(ReportSnapshot snapshot) => "mock.html";
        public string ExportCsv(ReportSnapshot snapshot) => "mock.csv";
        public string ExportJson(ReportSnapshot snapshot) => "mock.json";
    }

    private readonly MockInstalledProgramService _programService = new();
    private readonly MockUsageRepository _usageRepository = new();
    private readonly MockProgramUsageTracker _usageTracker = new();
    private readonly MockProgramProcessMatcher _processMatcher = new();
    private readonly MockCleanupRecommendationEngine _recommendationEngine = new();
    private readonly MockLlmExplanationService _llmExplanationService = new();
    private readonly MockSafeDiskCleanupService _cleanupService = new();
    private readonly MockCleanupLogService _logService = new();
    private readonly MockReportGeneratorService _reportGenerator = new();

    private MainViewModel CreateMainViewModel()
    {
        return new MainViewModel(
            _programService,
            _usageRepository,
            _usageTracker,
            _processMatcher,
            _recommendationEngine,
            _llmExplanationService,
            _cleanupService,
            _logService,
            _reportGenerator
        );
    }

    [Fact]
    public void Dashboard_OpensWithEmptyData_CorrectDefaultCounts()
    {
        // Arrange & Act
        var mainVm = CreateMainViewModel();
        var dashboardVm = mainVm.Dashboard;

        // Assert
        Assert.Equal(0, dashboardVm.TotalProgramCount);
        Assert.Equal(0, dashboardVm.UnusedProgramCount);
        Assert.Equal(0, dashboardVm.CleanupCandidateCount);
        Assert.Equal(0, dashboardVm.DependencySensitiveCount);
        Assert.Equal(0, dashboardVm.CleanableSpaceMB);
        Assert.Equal(0, dashboardVm.EstimatedReclaimableSpaceMB);
        Assert.Equal("0 MB", dashboardVm.ReclaimableSpaceText);
        Assert.Empty(dashboardVm.TopPrograms);
        Assert.Empty(dashboardVm.RecentPrograms);
    }

    [Fact]
    public void Dashboard_WithLargeData_CorrectCalculationsAndCounts()
    {
        // Arrange
        // Program 1: Large candidate, unused
        var p1 = new InstalledProgram { DisplayName = "Large Candidate App", EstimatedSizeMB = 2048 };
        _programService.Programs.Add(p1);
        _processMatcher.Matches[p1.DisplayName] = new ProgramProcessMatch { LastSeenUtc = null };
        _recommendationEngine.Recommendations[p1.DisplayName] = new CleanupRecommendation { Recommendation = "CleanupCandidate" };

        // Program 2: Dependency risk app, recently used
        var p2 = new InstalledProgram { DisplayName = "Critical Library", EstimatedSizeMB = 100 };
        _programService.Programs.Add(p2);
        _processMatcher.Matches[p2.DisplayName] = new ProgramProcessMatch { LastSeenUtc = DateTime.UtcNow };
        _recommendationEngine.Recommendations[p2.DisplayName] = new CleanupRecommendation { Recommendation = "DoNotRemove", DependencyRiskScore = 80 };

        // Program 3: Normal app, unused (last used 200 days ago)
        var p3 = new InstalledProgram { DisplayName = "Unused Normal App", EstimatedSizeMB = 500 };
        _programService.Programs.Add(p3);
        _processMatcher.Matches[p3.DisplayName] = new ProgramProcessMatch { LastSeenUtc = DateTime.UtcNow.AddDays(-200) };
        _recommendationEngine.Recommendations[p3.DisplayName] = new CleanupRecommendation { Recommendation = "Review" };

        // Disk cleanup targets (e.g. 512 MB temporary files)
        _cleanupService.Categories.Add(new CleanableCategory { CategoryName = "User Temp", TotalSizeMB = 512, CanClean = true });

        // Act
        var mainVm = CreateMainViewModel(); // LoadPrograms is automatically called in constructor
        mainVm.ScanCleanupCommand.Execute(null);
        var dashboardVm = mainVm.Dashboard;

        // Assert
        Assert.Equal(3, dashboardVm.TotalProgramCount);
        Assert.Equal(2, dashboardVm.UnusedProgramCount); // p1 (null) and p3 (200 days ago)
        Assert.Equal(1, dashboardVm.CleanupCandidateCount); // p1
        Assert.Equal(1, dashboardVm.DependencySensitiveCount); // p2
        Assert.Equal(512, dashboardVm.CleanableSpaceMB);
        // Estimated reclaimable space: 512 MB (temp) + 2048 MB (p1) = 2560 MB
        Assert.Equal(2560, dashboardVm.EstimatedReclaimableSpaceMB);
        // 2560 MB is 2.5 GB (2560 / 1024 = 2.5)
        Assert.Equal("2.5 GB", dashboardVm.ReclaimableSpaceText);

        // Check Top programs sorting and limit (max 10)
        Assert.Equal(3, dashboardVm.TopPrograms.Count);
        Assert.Equal("Large Candidate App", dashboardVm.TopPrograms[0].DisplayName); // 2048 MB
        Assert.Equal("Unused Normal App", dashboardVm.TopPrograms[1].DisplayName); // 500 MB
        Assert.Equal("Critical Library", dashboardVm.TopPrograms[2].DisplayName); // 100 MB

        // Check Recent programs (only those with usage last seen, ordered descending)
        Assert.Equal(2, dashboardVm.RecentPrograms.Count); // p2 and p3 (p1 has null last used, so not in recently used list)
        Assert.Equal("Critical Library", dashboardVm.RecentPrograms[0].DisplayName); // UtcNow (recent)
        Assert.Equal("Unused Normal App", dashboardVm.RecentPrograms[1].DisplayName); // 200 days ago
    }

    [Fact]
    public void Dashboard_NavigateToFilterCommand_AppliesPreAppliedFiltersAndNavigates()
    {
        // Arrange
        var mainVm = CreateMainViewModel();
        var dashboardVm = mainVm.Dashboard;
        var installedAppsVm = mainVm.InstalledApps;

        // Ensure "Installed Apps" navigation item is defined
        var targetNavItem = mainVm.NavigationItems.FirstOrDefault(item => item.Page == installedAppsVm);
        Assert.NotNull(targetNavItem);

        // Case 1: Unused filter
        dashboardVm.NavigateToFilterCommand.Execute("Unused");
        Assert.True(installedAppsVm.ShowUnusedPrograms);
        Assert.False(installedAppsVm.ShowLargePrograms);
        Assert.False(installedAppsVm.ShowCleanupCandidates);
        Assert.Equal(targetNavItem, mainVm.SelectedNavigationItem);

        // Case 2: Large filter
        dashboardVm.NavigateToFilterCommand.Execute("Large");
        Assert.False(installedAppsVm.ShowUnusedPrograms);
        Assert.True(installedAppsVm.ShowLargePrograms);
        Assert.False(installedAppsVm.ShowCleanupCandidates);

        // Case 3: Cleanup candidates filter
        dashboardVm.NavigateToFilterCommand.Execute("CleanupCandidates");
        Assert.False(installedAppsVm.ShowUnusedPrograms);
        Assert.False(installedAppsVm.ShowLargePrograms);
        Assert.True(installedAppsVm.ShowCleanupCandidates);

        // Case 4: System & SDK components
        dashboardVm.NavigateToFilterCommand.Execute("SystemSdk");
        Assert.False(installedAppsVm.ShowCleanupCandidates);
        Assert.True(installedAppsVm.ShowSdkRuntime);
        Assert.True(installedAppsVm.ShowSystemComponents);
    }
}
