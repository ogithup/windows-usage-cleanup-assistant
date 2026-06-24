using System.Windows;
using WindowsUsageCleanupAssistant.ViewModels;

namespace WindowsUsageCleanupAssistant;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
