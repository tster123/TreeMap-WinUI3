using Avalonia.Controls;
using AvaloniaUI.ViewModels;

namespace AvaloniaUI.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Main.ViewModel = viewModel;
    }
}
