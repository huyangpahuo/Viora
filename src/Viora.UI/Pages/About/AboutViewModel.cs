using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Viora.Core.Diagnostics;
using Viora.UI.Localization;

namespace Viora.UI.Pages.About;

public sealed partial class ThirdPartyComponent : ObservableObject
{
    public ThirdPartyComponent(string name, string license, string url)
    {
        Name = name;
        License = license;
        Url = url;
        Display = $"{name} — {license} ({url})";
    }

    public string Name { get; }

    public string License { get; }

    public string Url { get; }

    public string Display { get; }
}

/// <summary>
/// 关于页(合并了原「帮助」「反馈」两页的全部内容):应用信息、文档链接、
/// 反馈入口、更新检查、第三方组件与许可证。
/// </summary>
public partial class AboutViewModel : Viora.UI.Pages.PageViewModel
{
    private readonly ILogFileProvider _logFiles;

    public AboutViewModel(ILogFileProvider logFiles)
    {
        _logFiles = logFiles;

        var version = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0);
        VersionText = Tr.Format("About.Version", $"v{version.Major}.{version.Minor}.{version.Build}");

        ThirdPartyComponents = new ObservableCollection<ThirdPartyComponent>
        {
            new("CommunityToolkit.Mvvm", "MIT", "https://github.com/CommunityToolkit/dotnet"),
            new("Microsoft.Extensions.DependencyInjection", "MIT", "https://github.com/dotnet/runtime"),
            new("Microsoft.Extensions.Logging", "MIT", "https://github.com/dotnet/runtime"),
            new(".NET 8 (WPF)", "MIT", "https://dotnet.microsoft.com"),
        };
    }

    public override string TitleKey => "About.Title";

    public string VersionText { get; }

    public ObservableCollection<ThirdPartyComponent> ThirdPartyComponents { get; }

    [ObservableProperty]
    private string _updateButtonText = Tr.Get("About.CheckUpdates");

    [ObservableProperty]
    private string _updateStatusText = string.Empty;

    [RelayCommand]
    private void OpenProject() => Open(AppLinks.Repository);

    [RelayCommand]
    private void OpenSite() => Open(AppLinks.OfficialSite);

    [RelayCommand]
    private void OpenDocs() => Open(AppLinks.Wiki);

    [RelayCommand]
    private void OpenFaq() => Open(AppLinks.WikiFaq);

    [RelayCommand]
    private void OpenBug() => Open(AppLinks.BugReport);

    [RelayCommand]
    private void OpenFeature() => Open(AppLinks.FeatureRequest);

    [RelayCommand]
    private void OpenGeneral() => Open(AppLinks.Discussions);

    [RelayCommand]
    private void OpenLogs() =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_logFiles.Folder}\""));

    [RelayCommand]
    private async Task CheckUpdatesAsync()
    {
        // Honest behavior: no update server exists for this build (documented in About copy).
        UpdateButtonText = Tr.Get("About.CheckUpdates.Running");
        await Task.Delay(600); // makes the "checking" state perceivable, not fake success
        UpdateStatusText = Tr.Get("About.CheckUpdates.Manual");
        UpdateButtonText = Tr.Get("About.CheckUpdates");
        Open(AppLinks.Releases);
    }

    private static void Open(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
