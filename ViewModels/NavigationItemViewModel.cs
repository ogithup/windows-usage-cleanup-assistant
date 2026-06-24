namespace WindowsUsageCleanupAssistant.ViewModels;

public sealed class NavigationItemViewModel : ViewModelBase
{
    private bool _isSelected;

    public required string Title { get; init; }

    public required string Subtitle { get; init; }

    public required ViewModelBase Page { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
