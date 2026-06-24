using System.Collections.ObjectModel;
using WindowsUsageCleanupAssistant.Models;

namespace WindowsUsageCleanupAssistant.ViewModels;

public sealed class UsageViewModel : ViewModelBase
{
    private readonly MainViewModel _root;

    public UsageViewModel(MainViewModel root)
    {
        _root = root;
    }

    public ObservableCollection<UsageRecord> UsageRecords => _root.UsageRecords;

    public string StatusText => _root.UsageStatusText;

    public void Refresh()
    {
        OnPropertyChanged(nameof(UsageRecords));
        OnPropertyChanged(nameof(StatusText));
    }
}
