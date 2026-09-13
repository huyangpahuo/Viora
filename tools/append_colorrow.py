# -*- coding: utf-8 -*-
"""Append ThemeColorRow class."""
p = 'src/Viora.UI/Pages/Settings/SettingsViewModel.cs'
s = open(p, encoding='utf-8').read()
if 'class ThemeColorRow' in s:
    print('already present')
else:
    s += '''

/// <summary>One editable color row in the theme editor (label + hex + live swatch).</summary>
public sealed partial class ThemeColorRow : ObservableObject
{
    public static readonly (string Key, string Fallback)[] Fields =
    {
        ("Background", "#FF14141B"),
        ("Surface", "#FF1B1B23"),
        ("SurfaceElevated", "#FF23232E"),
        ("Primary", "#FF82B1FF"),
        ("OnPrimary", "#FF0031CB"),
        ("PrimaryContainer", "#FF1E41AF"),
        ("TextPrimary", "#FFE4E1F0"),
        ("TextSecondary", "#FFA9A7BD"),
        ("Outline", "#FF4A4A60"),
    };

    private readonly string _labelKey;

    public ThemeColorRow(string key, string hex)
    {
        _labelKey = key;
        _hex = hex;
        Viora.UI.Localization.LocalizationSource.Current.PropertyChanged += (_, _) =>
            OnPropertyChanged(nameof(Label));
    }

    public string Key => _labelKey;

    public string Label => Tr.Get("Theme.Color." + _labelKey);

    private string _hex;

    public string HexValue
    {
        get => _hex;
        set
        {
            if (SetProperty(ref _hex, value)) OnPropertyChanged(nameof(Brush));
        }
    }

    public System.Windows.Media.Brush Brush => Theming.ThemeCard.Swatch(HexValue);
}
'''
    open(p, 'w', encoding='utf-8').write(s)
    print('appended')
