using System.Windows.Controls;
using System.Windows.Data;
using DiveCenterManager.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace DiveCenterManager.Views;

public partial class ScheduleView : UserControl
{
    public ScheduleView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ScheduleViewModel vm)
            {
                var view = CollectionViewSource.GetDefaultView(vm.Rows);
                view.GroupDescriptions.Clear();
                view.GroupDescriptions.Add(new PropertyGroupDescription("ActivityDate"));
            }
        };
    }
    private async void Reservation_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox list && e.OriginalSource is DependencyObject source
            && ItemsControl.ContainerFromElement(list, source) is ListBoxItem
            && DataContext is ScheduleViewModel vm && vm.OpenReservationCommand.CanExecute(null))
        {
            e.Handled = true;
            await vm.OpenReservationCommand.ExecuteAsync();
        }
    }
}
