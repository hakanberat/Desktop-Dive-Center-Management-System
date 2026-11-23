using System.Windows;
using DiveCenterManager.ViewModels;
using DiveCenterManager.Views;

namespace DiveCenterManager.Services;

public sealed class DialogService
{
    public bool Edit(EditorViewModel viewModel)
    {
        var window = new EditorWindow { DataContext = viewModel };
        window.Owner = Application.Current.Windows.OfType<Window>().LastOrDefault(x => x.IsActive) ?? Application.Current.MainWindow;
        void Close(bool result) => window.DialogResult = result;
        viewModel.CloseRequested += Close;
        try { return window.ShowDialog() == true; }
        finally { viewModel.CloseRequested -= Close; }
    }
    public bool Confirm(string message) => MessageBox.Show(Application.Current.Windows.OfType<Window>().LastOrDefault(x => x.IsActive) ?? Application.Current.MainWindow, message, "Dive Hub Management", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
    public void Show(string title, string text) => MessageBox.Show(Application.Current.MainWindow, text, title, MessageBoxButton.OK, MessageBoxImage.Information);
    public async Task ShowScreenAsync(ScreenViewModel viewModel)
    {
        await viewModel.RefreshAsync();
        var window = new Window { Title = viewModel.Title, Width = 1180, Height = 780, MinWidth = 850, MinHeight = 600, Owner = Application.Current.MainWindow, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = new System.Windows.Controls.ContentControl { Content = viewModel, Margin = new Thickness(24) } };
        window.ShowDialog();
    }
}
