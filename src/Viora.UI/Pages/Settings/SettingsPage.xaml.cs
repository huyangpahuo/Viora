using System.Windows.Controls;

namespace Viora.UI.Pages.Settings;

/// <summary>设置页(通用/外观/性能/插件管理/关于)。数据与命令见 SettingsViewModel。</summary>
public partial class SettingsPage : UserControl
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
