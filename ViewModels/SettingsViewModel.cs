namespace WindowsUsageCleanupAssistant.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly MainViewModel _root;

    public SettingsViewModel(MainViewModel root)
    {
        _root = root;
    }

    public string ThemeMode => "Material Design / WPF";

    public string ExplanationProvider => "Mock LLM provider (safe, explanation only)";

    public string DatabasePath => _root.DatabasePath;

    public string CleanupLogPath => _root.CleanupLogPath;

    public string ReportsDirectoryPath => _root.ReportsDirectoryPath;

    public void Refresh()
    {
        OnPropertyChanged(nameof(DatabasePath));
        OnPropertyChanged(nameof(CleanupLogPath));
        OnPropertyChanged(nameof(ReportsDirectoryPath));
    }
}
