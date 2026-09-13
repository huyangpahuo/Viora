using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

public partial class AboutViewModel : Viora.UI.Pages.PageViewModel
{
    public AboutViewModel()
    {
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
    private void OpenProject() => Open("https://github.com/viora-project/viora");

    [RelayCommand]
    private async Task CheckUpdatesAsync()
    {
        // Honest behavior: no update server exists for this build (documented in About copy).
        UpdateButtonText = Tr.Get("About.CheckUpdates.Running");
        await Task.Delay(600); // makes the "checking" state perceivable, not fake success
        UpdateStatusText = Tr.Get("About.CheckUpdates.Manual");
        UpdateButtonText = Tr.Get("About.CheckUpdates");
    }

    private static void Open(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
