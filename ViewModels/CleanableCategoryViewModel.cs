using WindowsUsageCleanupAssistant.Models;

namespace WindowsUsageCleanupAssistant.ViewModels;

public sealed class CleanableCategoryViewModel : ViewModelBase
{
    private bool _isSelected;

    public required CleanableCategory Category { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string CategoryName => Category.CategoryName;

    public int FileCount => Category.FileCount;

    public double TotalSizeMB => Category.TotalSizeMB;

    public string RiskLevel => Category.RiskLevel;

    public string Description => Category.Description;

    public bool CanClean => Category.CanClean;
}
