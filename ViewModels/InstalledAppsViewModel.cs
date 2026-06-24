using System.ComponentModel;
using System.Windows.Data;

namespace WindowsUsageCleanupAssistant.ViewModels;

public sealed class InstalledAppsViewModel : ViewModelBase
{
    private readonly MainViewModel _root;
    private InstalledProgramViewModel? _selectedProgram;
    private string _searchText = string.Empty;
    private bool _showLargePrograms;
    private bool _showUnusedPrograms;
    private bool _showSdkRuntime;
    private bool _showGames;
    private bool _showDevelopmentTools;
    private bool _showSystemComponents;

    public InstalledAppsViewModel(MainViewModel root)
    {
        _root = root;
        ProgramsView = CollectionViewSource.GetDefaultView(_root.InstalledPrograms);
        ProgramsView.Filter = FilterProgram;
    }

    public ICollectionView ProgramsView { get; }

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

    public bool ShowLargePrograms
    {
        get => _showLargePrograms;
        set
        {
            if (SetProperty(ref _showLargePrograms, value))
            {
                Refresh();
            }
        }
    }

    public bool ShowUnusedPrograms
    {
        get => _showUnusedPrograms;
        set
        {
            if (SetProperty(ref _showUnusedPrograms, value))
            {
                Refresh();
            }
        }
    }

    public bool ShowSdkRuntime
    {
        get => _showSdkRuntime;
        set
        {
            if (SetProperty(ref _showSdkRuntime, value))
            {
                Refresh();
            }
        }
    }

    public bool ShowGames
    {
        get => _showGames;
        set
        {
            if (SetProperty(ref _showGames, value))
            {
                Refresh();
            }
        }
    }

    public bool ShowDevelopmentTools
    {
        get => _showDevelopmentTools;
        set
        {
            if (SetProperty(ref _showDevelopmentTools, value))
            {
                Refresh();
            }
        }
    }

    public bool ShowSystemComponents
    {
        get => _showSystemComponents;
        set
        {
            if (SetProperty(ref _showSystemComponents, value))
            {
                Refresh();
            }
        }
    }

    public string StatusText => _root.InventoryStatusText;

    public void Refresh()
    {
        ProgramsView.Refresh();
        OnPropertyChanged(nameof(StatusText));
    }

    private bool FilterProgram(object item)
    {
        if (item is not InstalledProgramViewModel program)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(SearchText) &&
            !program.DisplayName.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) &&
            !program.Publisher.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) &&
            !program.MatchedProcessName.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) &&
            !program.LlmExplanation.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase))
        {
            return false;
        }

        var filtersEnabled = ShowLargePrograms || ShowUnusedPrograms || ShowSdkRuntime || ShowGames || ShowDevelopmentTools || ShowSystemComponents;
        if (!filtersEnabled)
        {
            return true;
        }

        return MatchesLargePrograms(program) ||
               MatchesUnusedPrograms(program) ||
               MatchesSdkRuntime(program) ||
               MatchesGames(program) ||
               MatchesDevelopmentTools(program) ||
               MatchesSystemComponents(program);
    }

    private bool MatchesLargePrograms(InstalledProgramViewModel program) => ShowLargePrograms && program.EstimatedSizeMB >= 1000;

    private bool MatchesUnusedPrograms(InstalledProgramViewModel program) => ShowUnusedPrograms && (program.LastUsedUtc is null || (DateTime.UtcNow - program.LastUsedUtc.Value).TotalDays > 180);

    private bool MatchesSdkRuntime(InstalledProgramViewModel program) => ShowSdkRuntime && program.CategoryLabel == "SDK / Runtime";

    private bool MatchesGames(InstalledProgramViewModel program) => ShowGames && program.CategoryLabel == "Game";

    private bool MatchesDevelopmentTools(InstalledProgramViewModel program) => ShowDevelopmentTools && program.CategoryLabel == "Development Tool";

    private bool MatchesSystemComponents(InstalledProgramViewModel program) => ShowSystemComponents && program.CategoryLabel == "System Component";
}
