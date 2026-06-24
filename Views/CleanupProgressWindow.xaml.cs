using System.Windows;
using WindowsUsageCleanupAssistant.ViewModels;

namespace WindowsUsageCleanupAssistant.Views;

public partial class CleanupProgressWindow : Window
{
    public CleanupProgressWindow(CleanupProgressViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
