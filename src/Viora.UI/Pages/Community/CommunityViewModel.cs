using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Viora.UI.Localization;

namespace Viora.UI.Pages.Community;

/// <summary>
/// One community destination. Data-driven (brief §10): the list lives in code/data,
/// never hardcoded per-platform in UI; new destinations appear without XAML changes.
/// Empty placeholder entries are allowed until real spaces open.
/// </summary>
public sealed class CommunityDestination : ObservableObject
{
    public string Name { get; init; } = string.Empty;

    public string DescriptionKey { get; init; } = string.Empty;

    public string? Url { get; init; }

    public string Description => Tr.Get(DescriptionKey);

    public void Refresh() => OnPropertyChanged(nameof(Description));
}

public partial class CommunityViewModel : Viora.UI.Pages.PageViewModel
{
    public CommunityViewModel()
    {
        // Deliberately empty: no real community spaces exist yet. The mechanism is live —
        // add an entry here (QQ/Discord/Telegram/forum/website) and it renders automatically.
        Destinations = new ObservableCollection<CommunityDestination>();
    }

    public override string TitleKey => "Community.Title";

    public override string? SubtitleKey => "Community.Subtitle";

    public ObservableCollection<CommunityDestination> Destinations { get; }

    public bool HasNoDestinations => Destinations.Count == 0;

    [RelayCommand]
    private void Join(CommunityDestination? destination)
    {
        if (destination?.Url is { } url)
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
