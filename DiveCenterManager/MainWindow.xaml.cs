using System.Windows;
using DiveCenterManager.ViewModels;

namespace DiveCenterManager;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel) { InitializeComponent(); DataContext = viewModel; Loaded += (_, _) => viewModel.SelectedNavigation = viewModel.Navigation.FirstOrDefault(); }
    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
}
