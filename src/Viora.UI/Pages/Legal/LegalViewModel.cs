using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Viora.UI.Pages.Legal;

public sealed class OssComponent
{
    public OssComponent(string name, string license, string url) =>
        Display = $"{name} — {license} ({url})";

    public string Display { get; }
}

public partial class LegalViewModel : Viora.UI.Pages.PageViewModel
{
    public LegalViewModel()
    {
        Components = new ObservableCollection<OssComponent>
        {
            new("Viora", "GNU AGPL-3.0", AppLinks.Repository),
            new("CommunityToolkit.Mvvm", "MIT", "https://github.com/CommunityToolkit/dotnet"),
            new("Microsoft.Extensions.*", "MIT", "https://github.com/dotnet/runtime"),
        };
    }

    public override string TitleKey => "Legal.Title";

    public override string? SubtitleKey => "Legal.Subtitle";

    public ObservableCollection<OssComponent> Components { get; }
}
