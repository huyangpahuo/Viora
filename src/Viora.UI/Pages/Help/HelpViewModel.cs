using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using Viora.Core.Diagnostics;
using Viora.UI.Localization;

namespace Viora.UI.Pages.Help;

public partial class HelpViewModel : Viora.UI.Pages.PageViewModel
{
    private readonly ILogFileProvider _logFiles;

    public HelpViewModel(ILogFileProvider logFiles) => _logFiles = logFiles;

    public override string TitleKey => "Help.Title";

    public override string? SubtitleKey => "Help.Subtitle";

    [RelayCommand]
    private void OpenDocs() => Open("https://github.com/viora-project/viora/wiki");

    [RelayCommand]
    private void OpenFaq() => Open("https://github.com/viora-project/viora/wiki/FAQ");

    [RelayCommand]
    private void OpenWiki() => Open("https://github.com/viora-project/viora/wiki");

    [RelayCommand]
    private void OpenSite() => Open("https://viora.example.org");

    [RelayCommand]
    private void OpenLogs() =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_logFiles.Folder}\""));

    private static void Open(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
