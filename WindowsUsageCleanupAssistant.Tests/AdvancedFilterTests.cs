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

public class AdvancedFilterTests
{
    private class MockInstalledProgramService : IInstalledProgramService
    {
        public List<InstalledProgram> Programs { get; set; } = new();
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
        public event EventHandler? UsageUpdated { add { } remove { } }
        public void Start() { }
        public Task StopAsync() => Task.CompletedTask;
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
            return Task.FromResult("Mock Explanation");
        }
    }

    private class MockSafeDiskCleanupService : ISafeDiskCleanupService
    {
        public IReadOnlyList<CleanableCategory> Scan() => new List<CleanableCategory>();
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

    private void SetupMockData()
    {
        _programService.Programs.Clear();
        _processMatcher.Matches.Clear();
        _recommendationEngine.Recommendations.Clear();

        // P1: Large candidate, Game, high confidence match
        var p1 = new InstalledProgram { DisplayName = "Candidate Game App", Publisher = "Publisher A", EstimatedSizeMB = 2000 };
        _programService.Programs.Add(p1);
        _processMatcher.Matches[p1.DisplayName] = new ProgramProcessMatch { LastSeenUtc = DateTime.UtcNow, ConfidenceScore = 90 };
        _recommendationEngine.Recommendations[p1.DisplayName] = new CleanupRecommendation { Recommendation = "CleanupCandidate" };

        // P2: Small candidate, Game, low confidence match
        var p2 = new InstalledProgram { DisplayName = "Small Candidate Game", Publisher = "Publisher B", EstimatedSizeMB = 400 };
        _programService.Programs.Add(p2);
        _processMatcher.Matches[p2.DisplayName] = new ProgramProcessMatch { LastSeenUtc = DateTime.UtcNow, ConfidenceScore = 15 };
        _recommendationEngine.Recommendations[p2.DisplayName] = new CleanupRecommendation { Recommendation = "CleanupCandidate" };

        // P3: Medium kept, SDK, high confidence match, unused (200 days)
        var p3 = new InstalledProgram { DisplayName = "Kept SDK Library", Publisher = "Publisher C", EstimatedSizeMB = 1500 };
        _programService.Programs.Add(p3);
        _processMatcher.Matches[p3.DisplayName] = new ProgramProcessMatch { LastSeenUtc = DateTime.UtcNow.AddDays(-200), ConfidenceScore = 85 };
        _recommendationEngine.Recommendations[p3.DisplayName] = new CleanupRecommendation { Recommendation = "Keep" };

        // P4: Small review, Application, unmatched
        var p4 = new InstalledProgram { DisplayName = "Review App Utility", Publisher = "Publisher D", EstimatedSizeMB = 100 };
        _programService.Programs.Add(p4);
        _processMatcher.Matches[p4.DisplayName] = new ProgramProcessMatch { LastSeenUtc = null, ConfidenceScore = 0 };
        _recommendationEngine.Recommendations[p4.DisplayName] = new CleanupRecommendation { Recommendation = "Review" };
    }

    [Fact]
    public void AdvancedFilters_MultipleFiltersWorkingTogether_ReturnsCorrectSubset()
    {
        // Arrange
        SetupMockData();
        var mainVm = CreateMainViewModel();
        var vm = mainVm.InstalledApps;

        // Act & Assert 1: Only size filter (> 1 GB) -> P1 (2000MB) and P3 (1500MB)
        vm.SelectedSizeFilter = ">1GB";
        var filteredList = vm.ProgramsView.Cast<InstalledProgramViewModel>().ToList();
        Assert.Equal(2, filteredList.Count);
        Assert.Contains(filteredList, p => p.DisplayName == "Candidate Game App");
        Assert.Contains(filteredList, p => p.DisplayName == "Kept SDK Library");

        // Act & Assert 2: Size filter (> 1 GB) AND Category filter (Games) -> Only P1 (2000MB, Game)
        vm.ShowGames = true;
        filteredList = vm.ProgramsView.Cast<InstalledProgramViewModel>().ToList();
        Assert.Single(filteredList);
        Assert.Equal("Candidate Game App", filteredList[0].DisplayName);

        // Act & Assert 3: Size filter (> 1 GB) AND Category filter (Games) AND Recommendation (Keep) -> Empty (P1 is CleanupCandidate)
        vm.ShowRecKeep = true;
        filteredList = vm.ProgramsView.Cast<InstalledProgramViewModel>().ToList();
        Assert.Empty(filteredList);
    }

    [Fact]
    public void AdvancedFilters_SearchCategoryRecommendationClash_ReturnsCorrectSubset()
    {
        // Arrange
        SetupMockData();
        var mainVm = CreateMainViewModel();
        var vm = mainVm.InstalledApps;

        // Act: Search for "Candidate", Category: Game, Recommendation: CleanupCandidate
        vm.SearchText = "Candidate";
        vm.ShowGames = true;
        vm.ShowRecCleanupCandidate = true;

        // Assert: Matches P1 and P2
        var filteredList = vm.ProgramsView.Cast<InstalledProgramViewModel>().ToList();
        Assert.Equal(2, filteredList.Count);
        Assert.Contains(filteredList, p => p.DisplayName == "Candidate Game App");
        Assert.Contains(filteredList, p => p.DisplayName == "Small Candidate Game");

        // Act: Add Size filter (>1GB) on top of that
        vm.SelectedSizeFilter = ">1GB";
        filteredList = vm.ProgramsView.Cast<InstalledProgramViewModel>().ToList();
        Assert.Single(filteredList);
        Assert.Equal("Candidate Game App", filteredList[0].DisplayName);

        // Act: Change search query to a mismatching word
        vm.SearchText = "MissingWord";
        filteredList = vm.ProgramsView.Cast<InstalledProgramViewModel>().ToList();
        Assert.Empty(filteredList);
    }

    [Fact]
    public void AdvancedFilters_AfterCleanupOrRescan_UpdatesCollectionView()
    {
        // Arrange
        SetupMockData();
        var mainVm = CreateMainViewModel();
        var vm = mainVm.InstalledApps;

        // Initial check: 4 programs
        Assert.Equal(4, vm.ProgramsView.Cast<InstalledProgramViewModel>().Count());

        // Act: Simulate uninstall/cleanup of "Candidate Game App" by removing it from the mock service target
        var initialList = _programService.Programs.ToList();
        _programService.Programs.RemoveAll(p => p.DisplayName == "Candidate Game App");

        // Run the refresh command
        mainVm.RefreshCommand.Execute(null);

        // Assert: collection view should automatically update to contain 3 programs
        var updatedList = vm.ProgramsView.Cast<InstalledProgramViewModel>().ToList();
        Assert.Equal(3, updatedList.Count);
        Assert.DoesNotContain(updatedList, p => p.DisplayName == "Candidate Game App");
    }
}
