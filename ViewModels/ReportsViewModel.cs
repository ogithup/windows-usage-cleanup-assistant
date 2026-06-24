namespace WindowsUsageCleanupAssistant.ViewModels;

public sealed class ReportsViewModel : ViewModelBase
{
    private readonly MainViewModel _root;

    public ReportsViewModel(MainViewModel root)
    {
        _root = root;
    }

    public string StatusText => _root.ReportStatusText;

    public int ExportableProgramCount => _root.InstalledPrograms.Count;

    public double ReclaimableSpaceMB => _root.CleanableCategories.Sum(category => category.TotalSizeMB) +
                                        _root.InstalledPrograms.Where(program => program.Recommendation == "CleanupCandidate").Sum(program => program.EstimatedSizeMB ?? 0);

    public void Refresh()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ExportableProgramCount));
        OnPropertyChanged(nameof(ReclaimableSpaceMB));
    }
}
