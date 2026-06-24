using System.Collections.ObjectModel;

namespace WindowsUsageCleanupAssistant.ViewModels;

public sealed class CleanupViewModel : ViewModelBase
{
    private readonly MainViewModel _root;
    private bool _skipLockedFilesAutomatically = true;
    private int _lastSkippedEntries;

    public CleanupViewModel(MainViewModel root)
    {
        _root = root;
    }

    public ObservableCollection<CleanableCategoryViewModel> Categories => _root.CleanableCategories;

    public string StatusText => _root.CleanupStatusText;

    public double SelectedSpaceMB => Categories.Where(category => category.IsSelected && category.CanClean).Sum(category => category.TotalSizeMB);

    public bool SkipLockedFilesAutomatically
    {
        get => _skipLockedFilesAutomatically;
        set => SetProperty(ref _skipLockedFilesAutomatically, value);
    }

    public int LastSkippedEntries
    {
        get => _lastSkippedEntries;
        set
        {
            if (SetProperty(ref _lastSkippedEntries, value))
            {
                OnPropertyChanged(nameof(SkippedSummaryText));
            }
        }
    }

    public string SkippedSummaryText => LastSkippedEntries > 0
        ? $"Skipped {LastSkippedEntries} locked files"
        : "No locked files were skipped in the last cleanup run";

    public void Refresh()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(SelectedSpaceMB));
        OnPropertyChanged(nameof(SkippedSummaryText));
    }
}
