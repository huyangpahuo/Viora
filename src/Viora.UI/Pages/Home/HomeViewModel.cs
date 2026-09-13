using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Viora.UI.Shell;

namespace Viora.UI.Pages.Home;

public partial class HomeViewModel : PageViewModel
{
    private readonly ShellViewModel _shell;

    public HomeViewModel(ShellViewModel shell) => _shell = shell;

    public override string TitleKey => "Home.Title";

    public override string? SubtitleKey => "Home.Subtitle";

    [RelayCommand]
    private void GoConvert()
    {
        var item = _shell.Items.First(i => i.TitleKey == "Nav.Convert");
        _shell.SelectedItem = item;
    }

    [RelayCommand]
    private void GoPlugins()
    {
        var item = _shell.Items.First(i => i.TitleKey == "Nav.Plugins");
        _shell.SelectedItem = item;
    }
}
