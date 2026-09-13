using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using Viora.UI.Localization;

namespace Viora.UI.Pages.Sponsor;

public partial class SponsorViewModel : Viora.UI.Pages.PageViewModel
{
    public override string TitleKey => "Sponsor.Title";

    public override string? SubtitleKey => "Sponsor.Subtitle";

    [RelayCommand]
    private void OpenSponsor() =>
        Process.Start(new ProcessStartInfo("https://github.com/sponsors") { UseShellExecute = true });
}
