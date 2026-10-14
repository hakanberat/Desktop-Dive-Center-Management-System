using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DiveCenterManager.ViewModels;

namespace DiveCenterManager.Views;

public partial class TransactionsView : UserControl
{
    public TransactionsView() => InitializeComponent();
    private void OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(Records, source) is DataGridRow)
            EditSelected(e);
    }
    private void OnKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) EditSelected(e); }
    private void EditSelected(RoutedEventArgs e)
    {
        if (DataContext is TransactionViewModel vm && vm.EditCommand.CanExecute(null)) { e.Handled = true; vm.EditCommand.Execute(null); }
    }
}
