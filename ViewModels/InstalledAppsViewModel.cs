using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using WindowsUsageCleanupAssistant.Commands;

namespace WindowsUsageCleanupAssistant.ViewModels;

public sealed class InstalledAppsViewModel : ViewModelBase
{
    private readonly MainViewModel _root;
    private InstalledProgramViewModel? _selectedProgram;
    private string _searchText = string.Empty;
    
    private string _selectedSortOption = "Name";
    private string _selectedSizeFilter = "Any";
    private string _selectedLastUsedFilter = "Any";

    private bool _showCategorySdk;
    private bool _showCategoryDev;
    private bool _showCategoryGame;
    private bool _showCategoryApp;
    private bool _showCategorySys;

    private bool _showRecKeep;
    private bool _showRecReview;
    private bool _showRecCleanupCandidate;
    private bool _showRecDoNotRemove;

    private bool _showConfHigh;
    private bool _showConfMedium;
    private bool _showConfLow;
    private bool _showConfUnmatched;

    public InstalledAppsViewModel(MainViewModel root)
    {
        _root = root;
        ProgramsView = CollectionViewSource.GetDefaultView(_root.InstalledPrograms);
        ProgramsView.Filter = FilterProgram;
        
        ResetFiltersCommand = new RelayCommand(ResetFilters);
        ApplySorting();
    }

    public ICollectionView ProgramsView { get; }

    public ObservableCollection<string> ActiveFilters { get; } = new();

    public ICommand ResetFiltersCommand { get; }

    public InstalledProgramViewModel? SelectedProgram
    {
        get => _selectedProgram;
        set
        {
            if (SetProperty(ref _selectedProgram, value))
            {
                _root.NotifyInstalledProgramSelectionChanged();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                Refresh();
            }
        }
    }

    public string SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (SetProperty(ref _selectedSortOption, value))
            {
                ApplySorting();
            }
        }
    }

    public string SelectedSizeFilter
    {
        get => _selectedSizeFilter;
        set
        {
            if (SetProperty(ref _selectedSizeFilter, value))
            {
                OnPropertyChanged(nameof(ShowLargePrograms));
                Refresh();
            }
        }
    }

    public string SelectedLastUsedFilter
    {
        get => _selectedLastUsedFilter;
        set
        {
            if (SetProperty(ref _selectedLastUsedFilter, value))
            {
                OnPropertyChanged(nameof(ShowUnusedPrograms));
                Refresh();
            }
        }
    }

    // Toggle property mappings for backward compatibility

    public bool ShowLargePrograms
    {
        get => _selectedSizeFilter == ">1GB";
        set
        {
            if (value) SelectedSizeFilter = ">1GB";
            else SelectedSizeFilter = "Any";
        }
    }

    public bool ShowUnusedPrograms
    {
        get => _selectedLastUsedFilter == "180Days";
        set
        {
            if (value) SelectedLastUsedFilter = "180Days";
            else SelectedLastUsedFilter = "Any";
        }
    }

    public bool ShowSdkRuntime
    {
        get => _showCategorySdk;
        set
        {
            if (SetProperty(ref _showCategorySdk, value))
            {
                Refresh();
            }
        }
    }

    public bool ShowDevelopmentTools
    {
        get => _showCategoryDev;
        set
        {
            if (SetProperty(ref _showCategoryDev, value))
            {
                Refresh();
            }
        }
    }

    public bool ShowGames
    {
        get => _showCategoryGame;
        set
        {
            if (SetProperty(ref _showCategoryGame, value))
            {
                Refresh();
            }
        }
    }

    public bool ShowCategoryApplications
    {
        get => _showCategoryApp;
        set
        {
            if (SetProperty(ref _showCategoryApp, value))
            {
                Refresh();
            }
        }
    }

    public bool ShowSystemComponents
    {
        get => _showCategorySys;
        set
        {
            if (SetProperty(ref _showCategorySys, value))
            {
                Refresh();
            }
        }
    }

    public bool ShowRecKeep
    {
        get => _showRecKeep;
        set
        {
            if (SetProperty(ref _showRecKeep, value))
            {
                Refresh();
            }
        }
    }

    public bool ShowRecReview
    {
        get => _showRecReview;
        set
        {
            if (SetProperty(ref _showRecReview, value))
            {
                Refresh();
            }
        }
    }

    public bool ShowRecCleanupCandidate
    {
        get => _showRecCleanupCandidate;
        set
        {
            if (SetProperty(ref _showRecCleanupCandidate, value))
            {
                OnPropertyChanged(nameof(ShowCleanupCandidates));
                Refresh();
            }
        }
    }

    public bool ShowCleanupCandidates
    {
        get => _showRecCleanupCandidate;
        set
        {
            if (SetProperty(ref _showRecCleanupCandidate, value))
            {
                OnPropertyChanged(nameof(ShowRecCleanupCandidate));
                Refresh();
            }
        }
    }

    public bool ShowRecDoNotRemove
    {
        get => _showRecDoNotRemove;
        set
        {
            if (SetProperty(ref _showRecDoNotRemove, value))
            {
                Refresh();
            }
        }
    }

    public bool ShowConfHigh
    {
        get => _showConfHigh;
        set
        {
            if (SetProperty(ref _showConfHigh, value))
            {
                Refresh();
            }
        }
    }

    public bool ShowConfMedium
    {
        get => _showConfMedium;
        set
        {
            if (SetProperty(ref _showConfMedium, value))
            {
                Refresh();
            }
        }
    }

    public bool ShowConfLow
    {
        get => _showConfLow;
        set
        {
            if (SetProperty(ref _showConfLow, value))
            {
                Refresh();
            }
        }
    }

    public bool ShowConfUnmatched
    {
        get => _showConfUnmatched;
        set
        {
            if (SetProperty(ref _showConfUnmatched, value))
            {
                Refresh();
            }
        }
    }

    public string StatusText => _root.InventoryStatusText;

    public void ResetFilters()
    {
        _searchText = string.Empty;
        OnPropertyChanged(nameof(SearchText));
        
        _selectedSizeFilter = "Any";
        OnPropertyChanged(nameof(SelectedSizeFilter));
        OnPropertyChanged(nameof(ShowLargePrograms));

        _selectedLastUsedFilter = "Any";
        OnPropertyChanged(nameof(SelectedLastUsedFilter));
        OnPropertyChanged(nameof(ShowUnusedPrograms));

        _showCategorySdk = false;
        OnPropertyChanged(nameof(ShowSdkRuntime));
        _showCategoryDev = false;
        OnPropertyChanged(nameof(ShowDevelopmentTools));
        _showCategoryGame = false;
        OnPropertyChanged(nameof(ShowGames));
        _showCategoryApp = false;
        OnPropertyChanged(nameof(ShowCategoryApplications));
        _showCategorySys = false;
        OnPropertyChanged(nameof(ShowSystemComponents));

        _showRecKeep = false;
        OnPropertyChanged(nameof(ShowRecKeep));
        _showRecReview = false;
        OnPropertyChanged(nameof(ShowRecReview));
        _showRecCleanupCandidate = false;
        OnPropertyChanged(nameof(ShowRecCleanupCandidate));
        OnPropertyChanged(nameof(ShowCleanupCandidates));
        _showRecDoNotRemove = false;
        OnPropertyChanged(nameof(ShowRecDoNotRemove));

        _showConfHigh = false;
        OnPropertyChanged(nameof(ShowConfHigh));
        _showConfMedium = false;
        OnPropertyChanged(nameof(ShowConfMedium));
        _showConfLow = false;
        OnPropertyChanged(nameof(ShowConfLow));
        _showConfUnmatched = false;
        OnPropertyChanged(nameof(ShowConfUnmatched));

        Refresh();
    }

    public void Refresh()
    {
        ProgramsView.Refresh();
        UpdateActiveFilters();
        OnPropertyChanged(nameof(StatusText));
    }

    private void ApplySorting()
    {
        if (ProgramsView is ListCollectionView listCollectionView)
        {
            listCollectionView.CustomSort = new ProgramComparer(_selectedSortOption);
        }
    }

    private void UpdateActiveFilters()
    {
        ActiveFilters.Clear();
        if (!string.IsNullOrWhiteSpace(SearchText)) ActiveFilters.Add($"Arama: \"{SearchText}\"");
        if (SelectedSizeFilter != "Any") ActiveFilters.Add($"Boyut: {SelectedSizeFilter}");
        if (SelectedLastUsedFilter != "Any") ActiveFilters.Add($"Kullanım: {SelectedLastUsedFilter}");
        if (ShowSdkRuntime) ActiveFilters.Add("SDK / Runtime");
        if (ShowDevelopmentTools) ActiveFilters.Add("Dev Tool");
        if (ShowGames) ActiveFilters.Add("Game");
        if (ShowCategoryApplications) ActiveFilters.Add("Application");
        if (ShowSystemComponents) ActiveFilters.Add("System Component");
        if (ShowRecKeep) ActiveFilters.Add("Keep");
        if (ShowRecReview) ActiveFilters.Add("Review");
        if (ShowRecCleanupCandidate) ActiveFilters.Add("CleanupCandidate");
        if (ShowRecDoNotRemove) ActiveFilters.Add("DoNotRemove");
        if (ShowConfHigh) ActiveFilters.Add("Match: High");
        if (ShowConfMedium) ActiveFilters.Add("Match: Medium");
        if (ShowConfLow) ActiveFilters.Add("Match: Low");
        if (ShowConfUnmatched) ActiveFilters.Add("Match: Unmatched");
    }

    private bool FilterProgram(object item)
    {
        if (item is not InstalledProgramViewModel program)
        {
            return false;
        }

        // 1. Text Search
        if (!string.IsNullOrWhiteSpace(SearchText) &&
            !program.DisplayName.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) &&
            !program.Publisher.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) &&
            !program.MatchedProcessName.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) &&
            !program.LlmExplanation.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase))
        {
            return false;
        }

        // 2. Size Filter
        if (SelectedSizeFilter != "Any")
        {
            var size = program.EstimatedSizeMB ?? 0;
            if (SelectedSizeFilter == ">500MB" && size <= 500) return false;
            if (SelectedSizeFilter == ">1GB" && size <= 1024) return false;
            if (SelectedSizeFilter == ">5GB" && size <= 5120) return false;
        }

        // 3. Last Used Filter
        if (SelectedLastUsedFilter != "Any")
        {
            if (program.LastUsedUtc is null)
            {
                if (SelectedLastUsedFilter != "NoData") return false;
            }
            else
            {
                if (SelectedLastUsedFilter == "NoData") return false;
                var days = (DateTime.UtcNow - program.LastUsedUtc.Value).TotalDays;
                if (SelectedLastUsedFilter == "30Days" && days < 30) return false;
                if (SelectedLastUsedFilter == "180Days" && days < 180) return false;
                if (SelectedLastUsedFilter == "365Days" && days < 365) return false;
            }
        }

        // 4. Category Filter (multi-select group)
        var categoryFilterActive = ShowSdkRuntime || ShowDevelopmentTools || ShowGames || ShowCategoryApplications || ShowSystemComponents;
        if (categoryFilterActive)
        {
            var matchesCategory = (ShowSdkRuntime && program.CategoryLabel == "SDK / Runtime") ||
                                  (ShowDevelopmentTools && program.CategoryLabel == "Development Tool") ||
                                  (ShowGames && program.CategoryLabel == "Game") ||
                                  (ShowCategoryApplications && program.CategoryLabel == "Application") ||
                                  (ShowSystemComponents && program.CategoryLabel == "System Component");
            if (!matchesCategory) return false;
        }

        // 5. Recommendation Filter (multi-select group)
        var recFilterActive = ShowRecKeep || ShowRecReview || ShowRecCleanupCandidate || ShowRecDoNotRemove;
        if (recFilterActive)
        {
            var matchesRec = (ShowRecKeep && program.Recommendation == "Keep") ||
                             (ShowRecReview && program.Recommendation == "Review") ||
                             (ShowRecCleanupCandidate && program.Recommendation == "CleanupCandidate") ||
                             (ShowRecDoNotRemove && program.Recommendation == "DoNotRemove");
            if (!matchesRec) return false;
        }

        // 6. Match Confidence Filter (multi-select group)
        var confFilterActive = ShowConfHigh || ShowConfMedium || ShowConfLow || ShowConfUnmatched;
        if (confFilterActive)
        {
            var conf = program.MatchConfidence;
            var isHigh = conf >= 70;
            var isMedium = conf >= 30 && conf < 70;
            var isLow = conf > 0 && conf < 30;
            var isUnmatched = conf == 0;

            var matchesConf = (ShowConfHigh && isHigh) ||
                              (ShowConfMedium && isMedium) ||
                              (ShowConfLow && isLow) ||
                              (ShowConfUnmatched && isUnmatched);
            if (!matchesConf) return false;
        }

        return true;
    }

    private class ProgramComparer : System.Collections.IComparer
    {
        private readonly string _sortOption;

        public ProgramComparer(string sortOption)
        {
            _sortOption = sortOption;
        }

        public int Compare(object? x, object? y)
        {
            if (x is not InstalledProgramViewModel a || y is not InstalledProgramViewModel b)
            {
                return 0;
            }

            switch (_sortOption)
            {
                case "Size":
                    if (a.EstimatedSizeMB.HasValue && b.EstimatedSizeMB.HasValue)
                        return b.EstimatedSizeMB.Value.CompareTo(a.EstimatedSizeMB.Value);
                    if (a.EstimatedSizeMB.HasValue) return -1;
                    if (b.EstimatedSizeMB.HasValue) return 1;
                    return 0;

                case "LastUsed":
                    if (a.LastUsedUtc.HasValue && b.LastUsedUtc.HasValue)
                        return b.LastUsedUtc.Value.CompareTo(a.LastUsedUtc.Value);
                    if (a.LastUsedUtc.HasValue) return -1;
                    if (b.LastUsedUtc.HasValue) return 1;
                    return 0;

                case "LaunchCount":
                    return b.LaunchCount.CompareTo(a.LaunchCount);

                case "Score":
                    return b.FinalCleanupScore.CompareTo(a.FinalCleanupScore);

                default:
                    return string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCultureIgnoreCase);
            }
        }
    }
}
