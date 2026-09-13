using System.Windows.Controls;

namespace Viora.UI.Pages.Settings;

public partial class SettingsPage : UserControl
{
    public SettingsPage(SettingsViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }
}
