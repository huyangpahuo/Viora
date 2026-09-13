using CommunityToolkit.Mvvm.ComponentModel;
using Viora.UI.Localization;

namespace Viora.UI.Pages;

/// <summary>Base for page VMs: re-raises localized titles on language change.</summary>
public abstract class PageViewModel : ObservableObject
{
    protected PageViewModel()
    {
        LocalizationSource.Current.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Subtitle));
            OnLanguageChanged();
        };
    }

    public abstract string TitleKey { get; }

    public virtual string? SubtitleKey => null;

    public string Title => Tr.Get(TitleKey);

    public string? Subtitle => SubtitleKey is null ? null : Tr.Get(SubtitleKey);

    protected virtual void OnLanguageChanged() { }
}
