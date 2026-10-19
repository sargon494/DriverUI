using System.Windows.Controls;

namespace Ui.Wpf.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    private void IncrementInterval_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.SettingsViewModel vm && vm.CheckIntervalDays < 30)
        {
            vm.CheckIntervalDays++;
        }
    }

    private void DecrementInterval_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.SettingsViewModel vm && vm.CheckIntervalDays > 1)
        {
            vm.CheckIntervalDays--;
        }
    }
}
