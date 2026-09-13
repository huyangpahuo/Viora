using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using Viora.UI.Localization;

namespace Viora.UI.Pages.Feedback;

public partial class FeedbackViewModel : Viora.UI.Pages.PageViewModel
{
    public override string TitleKey => "Feedback.Title";

    public override string? SubtitleKey => "Feedback.Subtitle";

    [RelayCommand]
    private void OpenBug() => Open("https://github.com/viora-project/viora/issues/new?template=bug_report.md");

    [RelayCommand]
    private void OpenFeature() => Open("https://github.com/viora-project/viora/issues/new?template=feature_request.md");

    [RelayCommand]
    private void OpenGeneral() => Open("https://github.com/viora-project/viora/discussions");

    private static void Open(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
