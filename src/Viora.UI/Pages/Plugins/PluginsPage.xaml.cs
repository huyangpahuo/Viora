using System.Windows.Controls;

namespace Viora.UI.Pages.Plugins;

public partial class PluginsPage : UserControl
{
    public PluginsPage(PluginsViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        Loaded += async (_, _) => await vm.LoadedCommand.ExecuteAsync(null);
    }
}
