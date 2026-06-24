using System.Collections.ObjectModel;
using WindowsUsageCleanupAssistant.Models;

namespace WindowsUsageCleanupAssistant.ViewModels;

public sealed class CleanupProgressViewModel : ViewModelBase
{
    private string _currentStep = "Preparing cleanup...";
    private string _currentItem = string.Empty;
    private double _percentComplete;
    private int _processedSteps;
    private int _totalSteps;
    private int _deletedEntries;
    private int _skippedEntries;

    public CleanupProgressViewModel()
    {
        ActivityLog = [];
    }

    public ObservableCollection<string> ActivityLog { get; }

    public string CurrentStep
    {
        get => _currentStep;
        set => SetProperty(ref _currentStep, value);
    }

    public string CurrentItem
    {
        get => _currentItem;
        set => SetProperty(ref _currentItem, value);
    }

    public double PercentComplete
    {
        get => _percentComplete;
        set
        {
            if (SetProperty(ref _percentComplete, value))
            {
                OnPropertyChanged(nameof(PercentLabel));
            }
        }
    }

    public int ProcessedSteps
    {
        get => _processedSteps;
        set
        {
            if (SetProperty(ref _processedSteps, value))
            {
                OnPropertyChanged(nameof(StepLabel));
            }
        }
    }

    public int TotalSteps
    {
        get => _totalSteps;
        set
        {
            if (SetProperty(ref _totalSteps, value))
            {
                OnPropertyChanged(nameof(StepLabel));
            }
        }
    }

    public int DeletedEntries
    {
        get => _deletedEntries;
        set
        {
            if (SetProperty(ref _deletedEntries, value))
            {
                OnPropertyChanged(nameof(DeletedLabel));
            }
        }
    }

    public int SkippedEntries
    {
        get => _skippedEntries;
        set
        {
            if (SetProperty(ref _skippedEntries, value))
            {
                OnPropertyChanged(nameof(SkippedLabel));
            }
        }
    }

    public string PercentLabel => $"{PercentComplete:N1}% completed";

    public string StepLabel => $"Step {ProcessedSteps:N0} / {Math.Max(TotalSteps, 1):N0}";

    public string DeletedLabel => $"Deleted {DeletedEntries} items";

    public string SkippedLabel => $"Skipped {SkippedEntries} locked files";

    public void Apply(CleanupProgressUpdate update)
    {
        CurrentStep = update.CurrentStep;
        CurrentItem = update.CurrentItem;
        PercentComplete = update.PercentComplete;
        ProcessedSteps = update.ProcessedSteps;
        TotalSteps = update.TotalSteps;
        DeletedEntries = update.DeletedEntries;
        SkippedEntries = update.SkippedEntries;

        if (!string.IsNullOrWhiteSpace(update.CurrentItem))
        {
            ActivityLog.Insert(0, $"[{update.PercentComplete:N1}%] {update.CurrentStep}: {update.CurrentItem}");
            while (ActivityLog.Count > 100)
            {
                ActivityLog.RemoveAt(ActivityLog.Count - 1);
            }
        }
    }
}
