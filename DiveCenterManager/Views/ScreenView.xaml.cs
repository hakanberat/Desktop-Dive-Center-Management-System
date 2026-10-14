using System.Windows.Controls;
using System.Windows.Data;
using DiveCenterManager.ViewModels;
namespace DiveCenterManager.Views;
public partial class ScreenView : UserControl
{
    public ScreenView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ScreenViewModel vm)
            {
                var view = CollectionViewSource.GetDefaultView(vm.Rows);
                view.GroupDescriptions.Clear();
                if (vm is ScheduleViewModel) view.GroupDescriptions.Add(new PropertyGroupDescription("ActivityDate"));
            }
        };
    }
}
