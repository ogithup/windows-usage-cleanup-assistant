using System.Collections.ObjectModel;

namespace WindowsUsageCleanupAssistant.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
    private readonly MainViewModel _root;

    public DashboardViewModel(MainViewModel root)
    {
        _root = root;
        TopPrograms = new ObservableCollection<InstalledProgramViewModel>();
        CleanupHighlights = new ObservableCollection<CleanableCategoryViewModel>();
    }

    public ObservableCollection<InstalledProgramViewModel> TopPrograms { get; }

    public ObservableCollection<CleanableCategoryViewModel> CleanupHighlights { get; }

    public int TotalProgramCount => _root.InstalledPrograms.Count;

    public int UnusedProgramCount => _root.InstalledPrograms.Count(program => program.LastUsedUtc is null || (DateTime.UtcNow - program.LastUsedUtc.Value).TotalDays > 180);

    public int RiskyDependencyCount => _root.InstalledPrograms.Count(program => program.DependencyRiskScore > 0 || program.SystemRiskScore > 0);

    public double CleanableSpaceMB => _root.CleanableCategories.Sum(category => category.TotalSizeMB);

    public string DiskStatus => $"{CleanableSpaceMB:N1} MB safe cleanup preview";

    public void Refresh()
    {
        ReplaceCollection(TopPrograms, _root.InstalledPrograms
            .OrderByDescending(program => program.EstimatedSizeMB ?? 0)
            .Take(5));

        ReplaceCollection(CleanupHighlights, _root.CleanableCategories
            .OrderByDescending(category => category.TotalSizeMB)
            .Take(4));

        OnPropertyChanged(nameof(TotalProgramCount));
        OnPropertyChanged(nameof(UnusedProgramCount));
        OnPropertyChanged(nameof(RiskyDependencyCount));
        OnPropertyChanged(nameof(CleanableSpaceMB));
        OnPropertyChanged(nameof(DiskStatus));
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
