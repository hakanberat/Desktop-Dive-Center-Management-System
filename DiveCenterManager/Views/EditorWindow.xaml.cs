using System.Windows;
namespace DiveCenterManager.Views;
public partial class EditorWindow : Window
{
    public EditorWindow()
    {
        InitializeComponent();
        Closing += (_, e) => { if (DataContext is ViewModels.EditorViewModel { IsSaving: true }) e.Cancel = true; };
    }
}
