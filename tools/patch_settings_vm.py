# -*- coding: utf-8 -*-
"""Row-based theme editor for SettingsViewModel."""
p = 'src/Viora.UI/Pages/Settings/SettingsViewModel.cs'
s = open(p, encoding='utf-8').read()

old_fields = '''    [ObservableProperty]
    private bool _editingThemeVisible;

    [ObservableProperty]
    private string _editThemeId = string.Empty;   // empty = creating new

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private string _editBackground = "#FF14141B";

    [ObservableProperty]
    private string _editSurface = "#FF1B1B23";

    [ObservableProperty]
    private string _editSurfaceElevated = "#FF23232E";

    [ObservableProperty]
    private string _editPrimary = "#FF82B1FF";

    [ObservableProperty]
    private string _editOnPrimary = "#FF0031CB";

    [ObservableProperty]
    private string _editPrimaryContainer = "#FF1E41AF";

    [ObservableProperty]
    private string _editTextPrimary = "#FFE4E1F0";

    [ObservableProperty]
    private string _editTextSecondary = "#FFA9A7BD";

    [ObservableProperty]
    private string _editOutline = "#FF4A4A60";

    public bool EditingIsExisting => EditThemeId.Length > 0;

    partial void OnEditThemeIdChanged(string value) => OnPropertyChanged(nameof(EditingIsExisting));'''

new_fields = '''    [ObservableProperty]
    private bool _editingThemeVisible;

    [ObservableProperty]
    private string _editThemeId = string.Empty;   // empty = creating new

    [ObservableProperty]
    private string _editName = string.Empty;

    public System.Collections.ObjectModel.ObservableCollection<ThemeColorRow> EditColorRows { get; } = new();

    public bool EditingIsExisting => EditThemeId.Length > 0;

    partial void OnEditThemeIdChanged(string value) => OnPropertyChanged(nameof(EditingIsExisting));

    private void LoadEditorRows(IReadOnlyDictionary<string, string> colors)
    {
        string Get(string key, string fallback) => colors.TryGetValue(key, out var v) ? v : fallback;
        EditColorRows.Clear();
        foreach (var (key, fallback) in ThemeColorRow.Fields)
            EditColorRows.Add(new ThemeColorRow(key, Get(key, fallback)));
    }

    private System.Collections.Generic.Dictionary<string, string> CollectEditorRows()
    {
        var dict = new System.Collections.Generic.Dictionary<string, string>();
        foreach (var row in EditColorRows) dict[row.Key] = row.HexValue;
        return dict;
    }'''

assert old_fields in s, "fields block not found"
s = s.replace(old_fields, new_fields)

old_cmds_start = s.find('    [RelayCommand]\n    private void NewTheme()')
old_cmds_end = s.find('    /// <summary>Pushes all bound values into the settings service')
assert old_cmds_start > 0 and old_cmds_end > old_cmds_start

new_cmds = '''    [RelayCommand]
    private void NewTheme()
    {
        EditThemeId = string.Empty;
        EditName = Tr.Get("Theme.NewName");
        var current = Theming.ThemeManager.Schemes.FirstOrDefault(s => s.Id == Theming.ThemeManager.Current)
            ?? Theming.ThemeManager.Schemes[0];
        LoadEditorRows(new System.Collections.Generic.Dictionary<string, string>
        {
            ["Background"] = current.PreviewBackground,
            ["Surface"] = current.PreviewSurface,
            ["Primary"] = current.PreviewAccent,
        });
        EditingThemeVisible = true;
    }

    [RelayCommand]
    private void EditTheme(Theming.ThemeCard? card)
    {
        if (card?.Palette is null) return;
        EditThemeId = card.Palette.Id;
        EditName = card.Palette.Name;
        LoadEditorRows(card.Palette.Colors);
        EditingThemeVisible = true;
    }

    [RelayCommand]
    private void CancelEditTheme() => EditingThemeVisible = false;

    [RelayCommand]
    private void DeleteTheme(Theming.ThemeCard? card)
    {
        if (card?.Palette is null) return;
        _themeStore.Delete(card.Palette.Id);
        if (Theme == card.SchemeId)
        {
            Theme = Theming.ThemeManager.Dark;
            Apply();
            Theming.ThemeManager.Apply(Theming.ThemeManager.Dark);
        }
        RebuildThemeCards();
    }

    [RelayCommand]
    private void DeleteEditingTheme()
    {
        if (EditThemeId.Length == 0) return;
        _themeStore.Delete(EditThemeId);
        if (Theme == EditThemeId)
        {
            Theme = Theming.ThemeManager.Dark;
            Apply();
            Theming.ThemeManager.Apply(Theming.ThemeManager.Dark);
        }
        EditingThemeVisible = false;
        RebuildThemeCards();
    }

    [RelayCommand]
    private void SaveTheme()
    {
        string id = EditThemeId;
        if (id.Length == 0) id = "user-" + Guid.NewGuid().ToString("N")[..8];

        var palette = new Theming.UserPalette(
            id,
            string.IsNullOrWhiteSpace(EditName) ? Tr.Get("Theme.NewName") : EditName.Trim(),
            CollectEditorRows());

        _themeStore.Save(palette);
        EditingThemeVisible = false;
        RebuildThemeCards();

        Theme = id;
        Apply();
        Theming.ThemeManager.Apply(id);
        RefreshCardSelection();
    }

'''

s = s[:old_cmds_start] + new_cmds + s[old_cmds_end:]
open(p, 'w', encoding='utf-8').write(s)
print('vm editor rewritten')

# Append ThemeColorRow class at file end
s = open(p, encoding='utf-8').read()
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
print('ThemeColorRow appended')
