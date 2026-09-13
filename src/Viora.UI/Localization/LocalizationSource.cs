using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using Microsoft.Extensions.DependencyInjection;
using Viora.Core.Localization;

namespace Viora.UI.Localization;

/// <summary>
/// Notification hub between ILocalizationService and XAML bindings.
/// {loc:Translate} creates a Binding to this singleton; on LanguageChanged every
/// bound string re-evaluates live (no restart).
/// </summary>
public sealed class LocalizationSource : INotifyPropertyChanged
{
    private static LocalizationSource? _current;
    private static ILocalizationService? _service;

    public static LocalizationSource Current =>
        _current ?? throw new InvalidOperationException("LocalizationSource not initialized.");

    public static LocalizationSource Initialize(ILocalizationService service)
    {
        if (_current is not null) return _current;
        _service = service;
        _current = new LocalizationSource();
        service.LanguageChanged += (_, _) => _current.RaiseAll();
        return _current;
    }

    /// <summary>Localized string lookup; falls back to the key itself when unresolved (debug aid).</summary>
    public string this[string key] => _service?.GetString(key) ?? key;

    /// <summary>Current language code (bindable, e.g. for language list selection).</summary>
    public string Language => _service?.CurrentLanguage ?? "en";

    private void RaiseAll()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
    }

    /// <summary>Forces re-broadcast (e.g. after plugin strings register).</summary>
    public void Refresh() => RaiseAll();

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// XAML markup extension: Text="{loc:Translate Nav.Home}".
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TranslateExtension : MarkupExtension
{
    public TranslateExtension() { }

    public TranslateExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var target = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
        if (target?.TargetObject is DependencyObject && target.TargetProperty is not System.Windows.Controls.TextBlock)
        {
            // Normal path: bind so language switches update live.
        }

        // Design-time / no-source fallback returns the raw key.
        try
        {
            var binding = new Binding($"[{Key}]")
            {
                Mode = BindingMode.OneWay,
                Source = LocalizationSource.Current,
            };
            return binding.ProvideValue(serviceProvider);
        }
        catch (InvalidOperationException)
        {
            return Key; // LocalizationSource not initialized (designer/tests)
        }
    }
}
