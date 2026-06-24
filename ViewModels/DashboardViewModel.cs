using System.Collections.ObjectModel;
using System.Windows.Input;
using WindowsUsageCleanupAssistant.Commands;

namespace WindowsUsageCleanupAssistant.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
    private readonly MainViewModel _root;

    public DashboardViewModel(MainViewModel root)
    {
        _root = root;
        TopPrograms = new ObservableCollection<InstalledProgramViewModel>();
        RecentPrograms = new ObservableCollection<InstalledProgramViewModel>();
        CleanupHighlights = new ObservableCollection<CleanableCategoryViewModel>();
        NavigateToFilterCommand = new RelayCommand<string>(NavigateToFilter);
    }

    public ObservableCollection<InstalledProgramViewModel> TopPrograms { get; }

    public ObservableCollection<InstalledProgramViewModel> RecentPrograms { get; }

    public ObservableCollection<CleanableCategoryViewModel> CleanupHighlights { get; }

    public int TotalProgramCount => _root.InstalledPrograms.Count;

    public int UnusedProgramCount => _root.InstalledPrograms.Count(program => program.LastUsedUtc is null || (DateTime.UtcNow - program.LastUsedUtc.Value).TotalDays > 180);

    public int CleanupCandidateCount => _root.InstalledPrograms.Count(program => program.Recommendation == "CleanupCandidate");

    public int DependencySensitiveCount => _root.InstalledPrograms.Count(program => program.DependencyRiskScore > 0 || program.Recommendation == "DoNotRemove");

    public double CleanableSpaceMB => _root.CleanableCategories.Sum(category => category.TotalSizeMB);

    public double EstimatedReclaimableSpaceMB => CleanableSpaceMB + _root.InstalledPrograms
        .Where(program => program.Recommendation == "CleanupCandidate")
        .Sum(program => program.EstimatedSizeMB ?? 0);

    public string ReclaimableSpaceText
    {
        get
        {
            double mb = EstimatedReclaimableSpaceMB;
            if (mb >= 1024)
            {
                return $"{(mb / 1024.0).ToString("N1", System.Globalization.CultureInfo.InvariantCulture)} GB";
            }
            return $"{mb.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} MB";
        }
    }

    public string DiskStatus => $"{CleanableSpaceMB:N1} MB safe cleanup preview";

    public ICommand NavigateToFilterCommand { get; }

    public void Refresh()
    {
        ReplaceCollection(TopPrograms, _root.InstalledPrograms
            .OrderByDescending(program => program.EstimatedSizeMB ?? 0)
            .Take(10));

        ReplaceCollection(RecentPrograms, _root.InstalledPrograms
            .Where(program => program.LastUsedUtc.HasValue)
            .OrderByDescending(program => program.LastUsedUtc!.Value)
            .Take(10));

        ReplaceCollection(CleanupHighlights, _root.CleanableCategories
            .OrderByDescending(category => category.TotalSizeMB)
            .Take(4));

        OnPropertyChanged(nameof(TotalProgramCount));
        OnPropertyChanged(nameof(UnusedProgramCount));
        OnPropertyChanged(nameof(CleanupCandidateCount));
        OnPropertyChanged(nameof(DependencySensitiveCount));
        OnPropertyChanged(nameof(CleanableSpaceMB));
        OnPropertyChanged(nameof(EstimatedReclaimableSpaceMB));
        OnPropertyChanged(nameof(ReclaimableSpaceText));
        OnPropertyChanged(nameof(DiskStatus));
    }

    private void NavigateToFilter(string? filterType)
    {
        if (filterType == null) return;

        _root.InstalledApps.ResetFilters();

        switch (filterType)
        {
            case "Unused":
                _root.InstalledApps.ShowUnusedPrograms = true;
                break;
            case "Large":
                _root.InstalledApps.ShowLargePrograms = true;
                break;
            case "CleanupCandidates":
                _root.InstalledApps.ShowCleanupCandidates = true;
                break;
            case "SystemSdk":
                _root.InstalledApps.ShowSdkRuntime = true;
                _root.InstalledApps.ShowSystemComponents = true;
                break;
        }

        var navItem = _root.NavigationItems.FirstOrDefault(item => item.Page == _root.InstalledApps);
        if (navItem != null)
        {
            _root.SelectedNavigationItem = navItem;
        }
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }
}
