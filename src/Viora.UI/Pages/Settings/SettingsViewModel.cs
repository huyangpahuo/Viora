using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Viora.Core.Localization;
using Viora.Core.Settings;
using Viora.UI.Localization;
using Viora.UI.Services;

namespace Viora.UI.Pages.Settings;

public sealed class SettingCategory : ObservableObject
{
    public SettingCategory(string key, string glyph)
    {
        Key = key;
        Glyph = glyph;
        Title = Tr.Get(key);
    }

    public string Key { get; }

    public string Glyph { get; }

    public string Title { get; }

    public void Refresh() => OnPropertyChanged(nameof(Title));
}

/// <summary>
/// Settings page: nine categories per brief §9, each bound to a real, persisted setting.
/// Changes call ISettingsService.Update (debounced JSON persistence + live effect).
/// </summary>
public partial class SettingsViewModel : PageViewModel
{
    private readonly ISettingsService _settings;
    private readonly ILocalizationService _localization;
    private readonly Theming.IUserThemeStore _themeStore;
    private readonly IAppRestart? _restart;

    public SettingsViewModel(
        ISettingsService settings,
        ILocalizationService localization,
        Theming.IUserThemeStore themeStore,
        IEnumerable<SettingCategory> categories,
        IAppRestart? restart = null)
    {
        _settings = settings;
        _localization = localization;
        _themeStore = themeStore;
        _restart = restart;
        Categories = new ObservableCollection<SettingCategory>(categories);
        SelectedCategory = Categories.FirstOrDefault();
        RebuildThemeCards();

        LoadFromSettings();
        LocalizationSource.Current.PropertyChanged += (_, _) =>
        {
            foreach (var c in Categories) c.Refresh();
            OnPropertyChanged(nameof(ThemeCards));
        };
    }

    /// <summary>Theme cards (built-in + user) for the Appearance picker.</summary>
    public System.Collections.Generic.IEnumerable<Theming.ThemeCard> ThemeCards => _themeCards;

    internal readonly System.Collections.Generic.List<Theming.ThemeCard> _themeCards = new();

    private void RebuildThemeCards()
    {
        _themeCards.Clear();
        foreach (var scheme in Theming.ThemeManager.Schemes)
            _themeCards.Add(new Theming.ThemeCard(scheme));
        foreach (var palette in _themeStore.LoadAll())
            _themeCards.Add(new Theming.ThemeCard(palette));
        OnPropertyChanged(nameof(ThemeCards));
    }

    public override string TitleKey => "Settings.Title";

    public override string? SubtitleKey => "Settings.Subtitle";

    public ObservableCollection<SettingCategory> Categories { get; }

    [ObservableProperty]
    private SettingCategory? _selectedCategory;

    // General
    [ObservableProperty] private bool _startMaximized;
    [ObservableProperty] private bool _confirmClose;

    // Appearance
    [ObservableProperty] private string _theme = "dark";
    [ObservableProperty] private double _uiScale = 1.0;

    // Language
    [ObservableProperty] private string _language = "en";

    // Image processing
    [ObservableProperty] private int _previewMaxDimension = 1024;
    [ObservableProperty] private int _exportMaxDimension = 4096;
    [ObservableProperty] private int _paletteSize = 8;

    // Export
    [ObservableProperty] private string _defaultFormat = "png";
    [ObservableProperty] private int _jpegQuality = 92;

    // Plugins
    [ObservableProperty] private bool _enablePluginLoading = true;

    // Performance
    [ObservableProperty] private int _maxParallelism;
    [ObservableProperty] private bool _previewDuringInteraction = true;

    // Cache
    [ObservableProperty] private bool _enableCache = true;
    [ObservableProperty] private long _maxCacheSizeMb = 512;

    // Privacy
    [ObservableProperty] private bool _shareUsage;
    [ObservableProperty] private bool _redactPaths = true;

    // Developer
    [ObservableProperty] private bool _developerMode;
    [ObservableProperty] private string _logLevel = "Information";

    public string[] AvailableLanguages { get; } = { "en", "zh-Hans" };

    public string[] LogLevelOptions { get; } = { "Trace", "Debug", "Information", "Warning", "Error" };

    public string[] FormatOptions { get; } = { "png", "jpeg", "bmp" };



    partial void OnSelectedCategoryChanged(SettingCategory? value)
    {
        OnPropertyChanged(nameof(IsCategoryGeneral));
        OnPropertyChanged(nameof(IsCategoryAppearance));
        OnPropertyChanged(nameof(IsCategoryLanguage));
        OnPropertyChanged(nameof(IsCategoryProcessing));
        OnPropertyChanged(nameof(IsCategoryExport));
        OnPropertyChanged(nameof(IsCategoryPlugins));
        OnPropertyChanged(nameof(IsCategoryPerformance));
        OnPropertyChanged(nameof(IsCategoryCache));
        OnPropertyChanged(nameof(IsCategoryPrivacy));
        OnPropertyChanged(nameof(IsCategoryDeveloper));
    }

    // Any bound value change → push to settings (debounced persistence + live effect).
    private void OnAnyChanged(string? propertyName = null)
    {
        if (_loading) return;
        Apply();
    }

    public bool IsCategory(string key) => SelectedCategory?.Key == key;

    public bool IsCategoryGeneral => IsCategory("Settings.Group.General");
    public bool IsCategoryAppearance => IsCategory("Settings.Group.Appearance");
    public bool IsCategoryLanguage => IsCategory("Settings.Group.Language");
    public bool IsCategoryProcessing => IsCategory("Settings.Group.ImageProcessing");
    public bool IsCategoryExport => IsCategory("Settings.Group.Export");
    public bool IsCategoryPlugins => IsCategory("Settings.Group.Plugins");
    public bool IsCategoryPerformance => IsCategory("Settings.Group.Performance");
    public bool IsCategoryCache => IsCategory("Settings.Group.Cache");
    public bool IsCategoryPrivacy => IsCategory("Settings.Group.Privacy");
    public bool IsCategoryDeveloper => IsCategory("Settings.Group.Developer");

    private bool _loading;

    private void LoadFromSettings()
    {
        _loading = true;
        var s = _settings.Current;
        StartMaximized = s.General.StartMaximized;
        ConfirmClose = s.General.ConfirmBeforeCloseDuringProcessing;
        Theme = s.Appearance.Theme;
        UiScale = s.Appearance.UiScale;
        Language = s.Language.Language;
        PreviewMaxDimension = s.ImageProcessing.PreviewMaxDimension;
        ExportMaxDimension = s.ImageProcessing.ExportMaxDimension;
        PaletteSize = s.ImageProcessing.PaletteSize;
        DefaultFormat = s.Export.DefaultFormat;
        JpegQuality = s.Export.JpegQuality;
        EnablePluginLoading = s.Plugins.EnablePluginLoading;
        MaxParallelism = s.Performance.MaxDegreeOfParallelism;
        PreviewDuringInteraction = s.Performance.UsePreviewQualityDuringInteraction;
        EnableCache = s.Cache.EnableCache;
        MaxCacheSizeMb = s.Cache.MaxCacheSizeMb;
        ShareUsage = s.Privacy.ShareAnonymousUsage;
        RedactPaths = s.Privacy.RedactPathsInLogs;
        DeveloperMode = s.Debug.DeveloperMode;
        LogLevel = s.Debug.LogLevel;
        _loading = false;

        // Property auto-apply wiring: every ObservableProperty partial-changed hook.
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is null) return;
            switch (e.PropertyName)
            {
                case nameof(StartMaximized): Apply(); break;
                case nameof(ConfirmClose): Apply(); break;
                case nameof(Theme): Apply(); break;
                case nameof(UiScale): Apply(); break;
                case nameof(Language): Apply(); break;
                case nameof(PreviewMaxDimension): Apply(); break;
                case nameof(ExportMaxDimension): Apply(); break;
                case nameof(PaletteSize): Apply(); break;
                case nameof(DefaultFormat): Apply(); break;
                case nameof(JpegQuality): Apply(); break;
                case nameof(EnablePluginLoading): Apply(); break;
                case nameof(MaxParallelism): Apply(); break;
                case nameof(PreviewDuringInteraction): Apply(); break;
                case nameof(EnableCache): Apply(); break;
                case nameof(MaxCacheSizeMb): Apply(); break;
                case nameof(ShareUsage): Apply(); break;
                case nameof(RedactPaths): Apply(); break;
                case nameof(DeveloperMode): Apply(); break;
                case nameof(LogLevel): Apply(); break;
            }
        };
    }

    /// <summary>Theme card click: apply live (persist happens via auto-apply + Save button).</summary>
    [RelayCommand]
    private void SelectTheme(Theming.ThemeCard? card)
    {
        if (card is null || Theme == card.SchemeId) return;
        Theme = card.SchemeId;
        Apply(); // persists via settings service + SettingsChanged → ThemeManager.Apply
        Theming.ThemeManager.Apply(card.SchemeId); // immediate visual feedback
        RefreshCardSelection();
    }

    private void RefreshCardSelection()
    {
        foreach (var c in _themeCards) c.RefreshSelection();
    }

    // ===== Custom theme editor =====

    [ObservableProperty]
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
    }

    [RelayCommand]
    private void NewTheme()
    {
        EditThemeId = string.Empty;
        EditName = Tr.Get("Theme.NewName");
        // Seed from the current palette's key families.
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

    /// <summary>Explicit save: persist now, reload packs/theme, refresh every binding.</summary>
    [RelayCommand]
    private async Task SaveAndApplyAsync()
    {
        Apply();
        await _settings.SaveAsync();
        _localization.SetLanguage(Language);
        LocalizationSource.Current.Refresh();
        Theming.ThemeManager.Apply(Theme);
        SettingsSaved = true;
        SavedToastVisible = true;
        await Task.Delay(2200);
        SavedToastVisible = false;
    }

    [ObservableProperty]
    private bool _savedToastVisible;

    [ObservableProperty]
    private bool _settingsSaved;

    /// <summary>Pushes all bound values into the settings service (debounced persistence + live effects).</summary>
    [RelayCommand]
    private void Apply()
    {
        _settings.Update(s =>
        {
            s.General.StartMaximized = StartMaximized;
            s.General.ConfirmBeforeCloseDuringProcessing = ConfirmClose;
            s.Appearance.Theme = Theme;
            s.Appearance.UiScale = UiScale;
            s.Language.Language = Language;
            s.ImageProcessing.PreviewMaxDimension = PreviewMaxDimension;
            s.ImageProcessing.ExportMaxDimension = ExportMaxDimension;
            s.ImageProcessing.PaletteSize = PaletteSize;
            s.Export.DefaultFormat = DefaultFormat;
            s.Export.JpegQuality = JpegQuality;
            s.Plugins.EnablePluginLoading = EnablePluginLoading;
            s.Performance.MaxDegreeOfParallelism = MaxParallelism;
            s.Performance.UsePreviewQualityDuringInteraction = PreviewDuringInteraction;
            s.Cache.EnableCache = EnableCache;
            s.Cache.MaxCacheSizeMb = MaxCacheSizeMb;
            s.Privacy.ShareAnonymousUsage = ShareUsage;
            s.Privacy.RedactPathsInLogs = RedactPaths;
            s.Debug.DeveloperMode = DeveloperMode;
            s.Debug.LogLevel = LogLevel;
        });
    }
}

public interface IAppRestart
{
    void Restart();
}


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
