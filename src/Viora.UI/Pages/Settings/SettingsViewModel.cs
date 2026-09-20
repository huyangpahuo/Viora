using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Viora.Core;
using Viora.Core.Localization;
using Viora.Core.Plugins;
using Viora.Core.Settings;
using Viora.Core.Works;
using Viora.UI.Localization;
using Viora.UI.Services;
using Viora.UI.Theming;

namespace Viora.UI.Pages.Settings;

/// <summary>设置页左侧子导航项。IsEnabled=false 表示数据层尚无对应功能(即将推出)。</summary>
public sealed partial class SettingsSectionViewModel : ObservableObject
{
    public SettingsSectionViewModel(string key, string titleKey, string subtitleKey, string iconKey, bool isEnabled = true)
    {
        Key = key;
        TitleKey = titleKey;
        SubtitleKey = subtitleKey;
        IconKey = iconKey;
        IsEnabled = isEnabled;
    }

    public string Key { get; }

    public string TitleKey { get; }

    public string SubtitleKey { get; }

    public string IconKey { get; }

    public bool IsEnabled { get; }

    [ObservableProperty]
    private bool _isSelected;

    public string Title => Tr.Get(TitleKey);

    public string Subtitle => SubtitleKey.Length == 0 ? string.Empty : Tr.Get(SubtitleKey);

    public void Refresh()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));
    }
}

/// <summary>插件管理列表行。</summary>
public sealed partial class InstalledPluginRowViewModel : ObservableObject
{
    public InstalledPluginRowViewModel(SettingsViewModel owner, string id, string name, string version, bool isEnabled, string stateText)
    {
        Owner = owner;
        Id = id;
        Name = name;
        Version = version;
        _isEnabled = isEnabled;
        _stateText = stateText;
    }

    public SettingsViewModel Owner { get; }

    public string Id { get; }

    public string Name { get; }

    public string Version { get; }

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private string _stateText;

    partial void OnIsEnabledChanged(bool value) => Owner.TogglePluginAsync(this).FireAndForget();

    [RelayCommand]
    private Task Toggle() => Owner.TogglePluginAsync(this);
}

/// <summary>语言下拉选项。</summary>
/// <summary>自定义主题编辑器的颜色行(键 + 本地化标签 + 十六进制值 + 实时色板)。</summary>
public sealed partial class ThemeColorRowViewModel : ObservableObject
{
    public ThemeColorRowViewModel(string key, string label, string hex)
    {
        Key = key;
        Label = label;
        _hexValue = hex;
    }

    public string Key { get; }

    public string Label { get; }

    [ObservableProperty]
    private string _hexValue;

    partial void OnHexValueChanged(string value) => OnPropertyChanged(nameof(Brush));

    public Brush Brush => ThemeCard.Swatch(HexValue);
}

/// <summary>快捷键设置行(录制态切换 + 当前组合键展示)。</summary>
public sealed partial class HotkeyRowViewModel : ObservableObject
{
    public HotkeyRowViewModel(string id, string labelKey, string gesture)
    {
        Id = id;
        LabelKey = labelKey;
        _gesture = string.IsNullOrWhiteSpace(gesture) ? "—" : gesture;
    }

    public string Id { get; }

    public string LabelKey { get; }

    [ObservableProperty]
    private string _gesture;

    [ObservableProperty]
    private bool _isRecording;
}

/// <summary>导出格式下拉选项。</summary>
public sealed record FormatOption(string Code, string DisplayNameKey);

public sealed record LanguageOption(string Code, string DisplayName);

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly ILocalizationService _localization;
    private readonly IPluginHost _pluginHost;
    private readonly IWorksStore _works;
    private readonly IUiAlert _alert;
    private readonly IUserThemeStore _themeStore;
    private readonly ILogger<SettingsViewModel> _logger;
    private bool _pluginRowsLoaded;
    private bool _hotkeyRowsLoaded;
    private readonly HotkeyService _hotkeys;

    public SettingsViewModel(
        ISettingsService settings,
        ILocalizationService localization,
        IPluginHost pluginHost,
        IWorksStore works,
        IUiAlert alert,
        IUserThemeStore themeStore,
        HotkeyService hotkeys,
        ILogger<SettingsViewModel> logger)
    {
        _settings = settings;
        _localization = localization;
        _pluginHost = pluginHost;
        _works = works;
        _alert = alert;
        _themeStore = themeStore;
        _hotkeys = hotkeys;
        _logger = logger;

        Sections = new ObservableCollection<SettingsSectionViewModel>
        {
            new("general", "Settings.General", "Settings.General.Sub", "Gear"),
            new("appearance", "Settings.Appearance", "Settings.Appearance.Sub", "Image"),
            new("performance", "Settings.Performance", "Settings.Performance.Sub", "Sliders"),
            new("plugins", "Settings.Plugins", "Settings.Plugins.Sub", "PuzzlePiece"),
            new("ai", "Settings.AI", "Settings.AI.Sub", "WandMagicSparkles", isEnabled: false),
            new("hotkeys", "Settings.Hotkeys", "Settings.Hotkeys.Sub", "List"),
            new("about", "Settings.About", "Settings.About.Sub", "CircleInfo"),
        };

        LanguageItems = _localization.AvailableLanguages
            .Select(code => new LanguageOption(code, code.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "中文(简体)" : "English"))
            .ToList();
        LogLevels = ["Trace", "Debug", "Information", "Warning", "Error"];

        SelectedSection = Sections[0];
        RebuildThemeCards();

        // 语言切换后刷新子导航标题/副标题与计算文案(XAML 绑定的是普通属性,不会自动更新)
        LocalizationSource.Current.PropertyChanged += (_, _) =>
        {
            foreach (var section in Sections) section.Refresh();
            OnPropertyChanged(nameof(RedactStateText));
            OnPropertyChanged(nameof(EnablePluginNote));
        };
    }

    // ---------- 子导航 ----------

    public ObservableCollection<SettingsSectionViewModel> Sections { get; }

    [ObservableProperty]
    private SettingsSectionViewModel _selectedSection;

    partial void OnSelectedSectionChanged(SettingsSectionViewModel value)
    {
        OnPropertyChanged(nameof(IsGeneralSelected));
        OnPropertyChanged(nameof(IsAppearanceSelected));
        OnPropertyChanged(nameof(IsPerformanceSelected));
        OnPropertyChanged(nameof(IsPluginsSelected));
        OnPropertyChanged(nameof(IsHotkeysSelected));
        OnPropertyChanged(nameof(IsAboutSelected));
        if (value?.Key == "plugins" && !_pluginRowsLoaded)
            _ = LoadPluginRowsAsync();
        if (value?.Key == "hotkeys" && !_hotkeyRowsLoaded)
            LoadHotkeyRows();
    }

    public bool IsGeneralSelected => SelectedSection?.Key == "general";

    public bool IsAppearanceSelected => SelectedSection?.Key == "appearance";

    public bool IsPerformanceSelected => SelectedSection?.Key == "performance";

    public bool IsPluginsSelected => SelectedSection?.Key == "plugins";

    public bool IsHotkeysSelected => SelectedSection?.Key == "hotkeys";

    public bool IsAboutSelected => SelectedSection?.Key == "about";

    // ---------- 通用 ----------

    public bool StartMaximized
    {
        get => _settings.Current.General.StartMaximized;
        set => _settings.Update(s => s.General.StartMaximized = value);
    }

    /// <summary>单张导入大小上限(MB);0 = 不限制。风格化页导入时校验。</summary>
    public int ImportMaxSizeMb
    {
        get => _settings.Current.General.ImportMaxSizeMb;
        set
        {
            var clamped = Math.Clamp(value, 0, 2048);
            _settings.Update(s => s.General.ImportMaxSizeMb = clamped);
            OnPropertyChanged(nameof(ImportMaxSizeMb));
        }
    }

    public IReadOnlyList<LanguageOption> LanguageItems { get; }

    public string SelectedLanguage
    {
        get => _settings.Current.Language.Language;
        set
        {
            if (string.IsNullOrEmpty(value) || value == _settings.Current.Language.Language) return;
            _settings.Update(s => s.Language.Language = value);
            _localization.SetLanguage(value);
            PromptRestart(Tr.Get("Settings.Language.RestartConfirm"));
        }
    }

    /// <summary>询问并执行应用重启(语言等需要重启完整生效的设置共用)。</summary>
    private void PromptRestart(string confirmMessage)
    {
        if (!_alert.Confirm(Tr.Get("Settings.Restart.Title"), confirmMessage)) return;
        AppRestart.Restart();
    }

    /// <summary>本机字体选项(按显示名排序)。</summary>
    public IReadOnlyList<LanguageOption> FontItems { get; } =
        System.Windows.Media.Fonts.SystemFontFamilies
            .Select(f => new LanguageOption(f.Source, f.FamilyNames.TryGetValue(
                System.Windows.Markup.XmlLanguage.GetLanguage("zh-hans"), out var zh) && !string.IsNullOrEmpty(zh) ? zh : f.Source))
            .OrderBy(f => f.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public string SelectedFontFamily
    {
        get => _settings.Current.General.FontFamily;
        set
        {
            if (value == _settings.Current.General.FontFamily) return;
            _settings.Update(s => s.General.FontFamily = value ?? string.Empty);
            OnPropertyChanged(nameof(SelectedFontFamily));
        }
    }

    /// <summary>界面缩放档位(选择块),字号随缩放变化,即时生效。</summary>
    public double UiScale
    {
        get => Math.Clamp(_settings.Current.Appearance.UiScale, 0.8, 1.2);
        set
        {
            var clamped = Math.Clamp(value, 0.8, 1.2);
            _settings.Update(s => s.Appearance.UiScale = clamped);
            OnPropertyChanged(nameof(UiScale));
        }
    }

    /// <summary>缩放预设档位(选择块)。</summary>
    public double[] UiScaleOptions { get; } = [0.8, 0.9, 1.0, 1.1, 1.2];

    // ---------- 隐私 ----------

    public bool RedactPathsInLogs
    {
        get => _settings.Current.Privacy.RedactPathsInLogs;
        set
        {
            _settings.Update(s => s.Privacy.RedactPathsInLogs = value);
            OnPropertyChanged(nameof(RedactStateText));
        }
    }

    public string RedactStateText =>
        RedactPathsInLogs ? Tr.Get("Settings.Privacy.On") : Tr.Get("Settings.Privacy.Off");

    // ---------- 调试 ----------

    public string[] LogLevels { get; }

    public string SelectedLogLevel
    {
        get => _settings.Current.Debug.LogLevel;
        set
        {
            if (string.IsNullOrEmpty(value) || value == _settings.Current.Debug.LogLevel) return;
            _settings.Update(s => s.Debug.LogLevel = value);
        }
    }

    public bool DeveloperMode
    {
        get => _settings.Current.Debug.DeveloperMode;
        set => _settings.Update(s => s.Debug.DeveloperMode = value);
    }

    // ---------- 外观主题 ----------

    /// <summary>主题卡(内置方案 + 用户自定义),统一绑定面(ThemeCard)。</summary>
    public ObservableCollection<ThemeCard> ThemeCards { get; } = new();

    public bool HasUserThemes => ThemeCards.Any(c => c.IsUser);

    private void RebuildThemeCards()
    {
        ThemeCards.Clear();
        foreach (var scheme in ThemeManager.Schemes) ThemeCards.Add(new ThemeCard(scheme));
        foreach (var palette in _themeStore.LoadAll()) ThemeCards.Add(new ThemeCard(palette));
        RefreshThemeSelection();
        OnPropertyChanged(nameof(HasUserThemes));
    }

    private void RefreshThemeSelection()
    {
        foreach (var card in ThemeCards) card.RefreshSelection();
    }

    [RelayCommand]
    private void ApplyThemeCard(ThemeCard? card)
    {
        if (card is null) return;
        ThemeManager.Apply(card.SchemeId);
        _settings.Update(s => s.Appearance.Theme = card.SchemeId);
        RefreshThemeSelection();
    }

    // 自定义主题编辑器:以当前主题五色为起点,改名字与色值后保存为用户主题
    [ObservableProperty]
    private bool _isThemeEditorOpen;

    [ObservableProperty]
    private string _editorName = string.Empty;

    public ObservableCollection<ThemeColorRowViewModel> EditorColorRows { get; } = new();

    private string? RowHex(string key) =>
        NormalizeHex(EditorColorRows.FirstOrDefault(r => r.Key == key)?.HexValue);

    [RelayCommand]
    private void StartNewTheme()
    {
        var current = ThemeCards.FirstOrDefault(c => c.IsSelected) ?? ThemeCards.FirstOrDefault();
        var background = current?.PreviewBackground ?? "#FF14141B";
        var surface = current?.PreviewSurface ?? "#FF1B1B23";
        var accent = current?.PreviewAccent ?? "#FF82B1FF";
        var container = current?.PreviewPrimaryContainer ?? "#FF1E41AF";
        var text = current?.PreviewTextPrimary ?? "#FFE4FBEA";
        SetEditorRows(background, surface, accent, container, text);
        EditorName = Tr.Get("Theme.NewName");
        IsThemeEditorOpen = true;
    }

    private void SetEditorRows(string background, string surface, string accent, string container, string text)
    {
        EditorColorRows.Clear();
        EditorColorRows.Add(new ThemeColorRowViewModel("Background", Tr.Get("Settings.Theme.ColorBackground"), background));
        EditorColorRows.Add(new ThemeColorRowViewModel("Surface", Tr.Get("Settings.Theme.ColorSurface"), surface));
        EditorColorRows.Add(new ThemeColorRowViewModel("Accent", Tr.Get("Settings.Theme.ColorAccent"), accent));
        EditorColorRows.Add(new ThemeColorRowViewModel("Container", Tr.Get("Settings.Theme.ColorContainer"), container));
        EditorColorRows.Add(new ThemeColorRowViewModel("Text", Tr.Get("Settings.Theme.ColorText"), text));
    }

    [RelayCommand]
    private void CancelThemeEditor() => IsThemeEditorOpen = false;

    private static string? NormalizeHex(string? hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex?.Trim() ?? string.Empty);
            return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        }
        catch
        {
            return null;
        }
    }

    [RelayCommand]
    private void SaveTheme()
    {
        var background = RowHex("Background");
        var surface = RowHex("Surface");
        var accent = RowHex("Accent");
        var container = RowHex("Container");
        var text = RowHex("Text");
        if (background is null || surface is null || accent is null || container is null || text is null)
        {
            _alert.Warn(Tr.Get("Settings.Theme.Save"), Tr.Get("Settings.Theme.InvalidColor"));
            return;
        }

        var name = string.IsNullOrWhiteSpace(EditorName) ? Tr.Get("Theme.NewName") : EditorName.Trim();
        var palette = new UserPalette(
            $"user:{DateTime.Now:yyyyMMddHHmmss}",
            name,
            new Dictionary<string, string>
            {
                ["Background"] = background,
                ["Surface"] = surface,
                ["Primary"] = accent,
                ["PrimaryContainer"] = container,
                ["TextPrimary"] = text,
            });
        _themeStore.Save(palette);
        ThemeManager.Apply(palette.Id);
        _settings.Update(s => s.Appearance.Theme = palette.Id);
        RebuildThemeCards();
        IsThemeEditorOpen = false;
    }

    [RelayCommand]
    private void DeleteTheme(ThemeCard? card)
    {
        if (card is not { IsUser: true }) return;
        if (!_alert.Confirm(Tr.Get("Theme.Delete"), string.Format(Tr.Get("Settings.Theme.DeleteConfirm"), card.Name))) return;
        _themeStore.Delete(card.SchemeId);
        if (ThemeManager.Current == card.SchemeId)
        {
            ThemeManager.Apply(ThemeManager.Dark);
            _settings.Update(s => s.Appearance.Theme = ThemeManager.Dark);
        }
        RebuildThemeCards();
    }

    // ---------- 性能 ----------

    public bool UsePreviewQualityDuringInteraction
    {
        get => _settings.Current.Performance.UsePreviewQualityDuringInteraction;
        set => _settings.Update(s => s.Performance.UsePreviewQualityDuringInteraction = value);
    }

    public int PreviewMaxDimension
    {
        get => _settings.Current.ImageProcessing.PreviewMaxDimension;
        set
        {
            var clamped = Math.Clamp(value, 256, 4096);
            _settings.Update(s => s.ImageProcessing.PreviewMaxDimension = clamped);
            OnPropertyChanged(nameof(PreviewMaxDimension));
        }
    }

    public int ExportMaxDimension
    {
        get => _settings.Current.ImageProcessing.ExportMaxDimension;
        set
        {
            var clamped = Math.Clamp(value, 1024, 16384);
            _settings.Update(s => s.ImageProcessing.ExportMaxDimension = clamped);
            OnPropertyChanged(nameof(ExportMaxDimension));
        }
    }

    /// <summary>导出格式选项(本地化显示名),风格化页导出对话框与编码器使用。</summary>
    public IReadOnlyList<FormatOption> ExportFormats { get; } =
    [
        new("png", "Export.Format.Png"),
        new("jpeg", "Export.Format.Jpeg"),
        new("bmp", "Export.Format.Bmp"),
        new("tiff", "Export.Format.Tiff"),
        new("gif", "Export.Format.Gif"),
        new("webp", "Export.Format.Webp"),
    ];

    public string SelectedExportFormat
    {
        get => _settings.Current.Export.DefaultFormat;
        set
        {
            if (string.IsNullOrEmpty(value) || value == _settings.Current.Export.DefaultFormat) return;
            _settings.Update(s => s.Export.DefaultFormat = value);
        }
    }

    public int JpegQuality
    {
        get => _settings.Current.Export.JpegQuality;
        set
        {
            var clamped = Math.Clamp(value, 1, 100);
            _settings.Update(s => s.Export.JpegQuality = clamped);
            OnPropertyChanged(nameof(JpegQuality));
        }
    }

    // ---------- 路径(日志 / 作品库,重启生效) ----------

    public string LogFolderDisplay => AppLocations.ResolveLogsFolder(_settings.Current.Debug.LogFolder);

    public bool LogFolderIsCustom => !string.IsNullOrWhiteSpace(_settings.Current.Debug.LogFolder);

    [RelayCommand]
    private void OpenLogFolder() => OpenInExplorer(LogFolderDisplay);

    [RelayCommand]
    private void ChooseLogFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = Tr.Get("Settings.Storage.Choose") };
        if (dialog.ShowDialog() != true) return;
        _settings.Update(s => s.Debug.LogFolder = dialog.FolderName);
        OnPropertyChanged(nameof(LogFolderDisplay));
        OnPropertyChanged(nameof(LogFolderIsCustom));
        _alert.Info(Tr.Get("Settings.RestartNote"));
    }

    [RelayCommand]
    private void ResetLogFolder()
    {
        _settings.Update(s => s.Debug.LogFolder = string.Empty);
        OnPropertyChanged(nameof(LogFolderDisplay));
        OnPropertyChanged(nameof(LogFolderIsCustom));
    }

    public string WorksFolderDisplay => AppLocations.ResolveWorksFolder(_settings.Current.General.WorksFolder);

    public bool WorksFolderIsCustom => !string.IsNullOrWhiteSpace(_settings.Current.General.WorksFolder);

    [RelayCommand]
    private void OpenWorksFolder() => OpenInExplorer(WorksFolderDisplay);

    [RelayCommand]
    private void ChooseWorksFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = Tr.Get("Settings.Storage.Choose") };
        if (dialog.ShowDialog() != true) return;
        _settings.Update(s => s.General.WorksFolder = dialog.FolderName);
        OnPropertyChanged(nameof(WorksFolderDisplay));
        OnPropertyChanged(nameof(WorksFolderIsCustom));
        _alert.Info(Tr.Get("Settings.RestartNote"));
    }

    [RelayCommand]
    private void ResetWorksFolder()
    {
        _settings.Update(s => s.General.WorksFolder = string.Empty);
        OnPropertyChanged(nameof(WorksFolderDisplay));
        OnPropertyChanged(nameof(WorksFolderIsCustom));
    }

    // ---------- 插件管理 ----------

    public bool EnablePluginLoading
    {
        get => _settings.Current.Plugins.EnablePluginLoading;
        set => _settings.Update(s => s.Plugins.EnablePluginLoading = value);
    }

    public string EnablePluginNote => Tr.Get("Settings.Plugins.EnableNote");

    public ObservableCollection<InstalledPluginRowViewModel> PluginRows { get; } = new();

    public bool HasPluginRows => PluginRows.Count > 0;

    private async Task LoadPluginRowsAsync()
    {
        try
        {
            var disabled = _settings.Current.Plugins.DisabledPlugins;
            var rows = (await _pluginHost.GetPluginsAsync())
                .Select(p => new InstalledPluginRowViewModel(
                    this,
                    p.Metadata.Id,
                    p.Metadata.DisplayName,
                    $"v{p.Metadata.Version.ToString(3)}",
                    isEnabled: p.State != PluginLoadState.Disabled && !disabled.Contains(p.Metadata.Id),
                    stateText: p.State switch
                    {
                        PluginLoadState.Enabled => Tr.Get("Settings.Plugins.State.Enabled"),
                        PluginLoadState.Disabled => Tr.Get("Settings.Plugins.State.Disabled"),
                        PluginLoadState.Failed => Tr.Get("Settings.Plugins.State.Failed"),
                        PluginLoadState.Incompatible => Tr.Get("Settings.Plugins.State.Incompatible"),
                        _ => Tr.Get("Settings.Plugins.State.Disabled"),
                    }))
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            PluginRows.Clear();
            foreach (var row in rows) PluginRows.Add(row);
            _pluginRowsLoaded = true;
            OnPropertyChanged(nameof(HasPluginRows));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list installed plugins in settings");
        }
    }

    public async Task TogglePluginAsync(InstalledPluginRowViewModel row)
    {
        try
        {
            if (row.IsEnabled)
                await _pluginHost.EnableAsync(row.Id);
            else
                await _pluginHost.DisableAsync(row.Id);
            row.StateText = row.IsEnabled
                ? Tr.Get("Settings.Plugins.State.Enabled")
                : Tr.Get("Settings.Plugins.State.Disabled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle plugin {PluginId}", row.Id);
            _alert.Warn(Tr.Get("Settings.Plugins.ToggleFailed.Title"), Tr.Get("Settings.Plugins.ToggleFailed.Body"));
            // 回滚开关视觉
            row.IsEnabled = !row.IsEnabled;
        }
    }

    // ---------- 快捷键 ----------

    public ObservableCollection<HotkeyRowViewModel> HotkeyRows { get; } = new();

    private void LoadHotkeyRows()
    {
        HotkeyRows.Clear();
        foreach (var def in HotkeyService.Defaults)
            HotkeyRows.Add(new HotkeyRowViewModel(def.Id, def.LabelKey, _hotkeys.GetGesture(def.Id)));
        _hotkeyRowsLoaded = true;
    }

    [RelayCommand]
    private void StartRecord(HotkeyRowViewModel? row)
    {
        if (row is null) return;
        foreach (var r in HotkeyRows)
            if (r.IsRecording) r.IsRecording = false;
        row.IsRecording = true;
    }

    /// <summary>录制中的按键处理(由页面 PreviewKeyDown 调用)。返回 true = 已处理。</summary>
    public bool HandleRecordKey(System.Windows.Input.KeyEventArgs e)
    {
        var row = HotkeyRows.FirstOrDefault(r => r.IsRecording);
        if (row is null) return false;

        var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
        var input = System.Windows.Input.Keyboard.Modifiers;

        if (key is System.Windows.Input.Key.Escape)
        {
            row.IsRecording = false; // Esc 取消
            return true;
        }
        if (key is System.Windows.Input.Key.LeftCtrl or System.Windows.Input.Key.RightCtrl
            or System.Windows.Input.Key.LeftShift or System.Windows.Input.Key.RightShift
            or System.Windows.Input.Key.LeftAlt or System.Windows.Input.Key.RightAlt
            or System.Windows.Input.Key.LWin or System.Windows.Input.Key.RWin)
            return true; // 纯修饰键:等待组合

        // Delete/Backspace 清除绑定
        if (input == System.Windows.Input.ModifierKeys.None
            && key is System.Windows.Input.Key.Delete or System.Windows.Input.Key.Back)
        {
            ApplyGesture(row, string.Empty);
            row.IsRecording = false;
            return true;
        }

        // 必须带修饰键(功能键除外),避免抢占普通输入
        if (input == System.Windows.Input.ModifierKeys.None
            && key is not (>= System.Windows.Input.Key.F1 and <= System.Windows.Input.Key.F24))
        {
            row.IsRecording = false;
            _alert.Warn(Tr.Get("Settings.Hotkeys.ConflictTitle"), Tr.Get("Settings.Hotkeys.NeedModifier"));
            return true;
        }

        var parts = new List<string>();
        if (input.HasFlag(System.Windows.Input.ModifierKeys.Control)) parts.Add("Ctrl");
        if (input.HasFlag(System.Windows.Input.ModifierKeys.Alt)) parts.Add("Alt");
        if (input.HasFlag(System.Windows.Input.ModifierKeys.Shift)) parts.Add("Shift");
        parts.Add(KeyToToken(key));
        ApplyGesture(row, string.Join("+", parts));
        row.IsRecording = false;
        return true;
    }

    private void ApplyGesture(HotkeyRowViewModel row, string gesture)
    {
        var conflict = _hotkeys.SetGesture(row.Id, gesture);
        if (conflict is not null)
        {
            _alert.Warn(Tr.Get("Settings.Hotkeys.ConflictTitle"),
                string.Format(Tr.Get("Settings.Hotkeys.Conflict"), Tr.Get(conflict.LabelKey)));
            row.Gesture = _hotkeys.GetGesture(row.Id);
            return;
        }
        row.Gesture = string.IsNullOrWhiteSpace(gesture) ? "—" : gesture;
    }

    private static string KeyToToken(System.Windows.Input.Key key) => key switch
    {
        >= System.Windows.Input.Key.D0 and <= System.Windows.Input.Key.D9
            => ((int)(key - System.Windows.Input.Key.D0)).ToString(),
        >= System.Windows.Input.Key.NumPad0 and <= System.Windows.Input.Key.NumPad9
            => ((int)(key - System.Windows.Input.Key.NumPad0)).ToString(),
        _ => key.ToString(),
    };

    [RelayCommand]
    private void ResetHotkeys()
    {
        foreach (var row in HotkeyRows)
        {
            _hotkeys.SetGesture(row.Id, string.Empty);
            row.Gesture = _hotkeys.GetGesture(row.Id);
            row.IsRecording = false;
        }
    }

    // ---------- 关于 ----------

    public string AppVersion
    {
        get
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version;
            return "Viora " + (version is null ? "1.0" : version.ToString(3));
        }
    }

    public const string RepoUrl = "https://github.com/huyangpahuo/Viora";
    public const string ReleasesUrl = "https://github.com/huyangpahuo/Viora/releases/latest";

    private static readonly System.Net.Http.HttpClient UpdateClient = CreateUpdateClient();

    private static System.Net.Http.HttpClient CreateUpdateClient()
    {
        var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Viora/1.0 (+https://github.com/huyangpahuo/Viora)");
        return client;
    }

    [ObservableProperty]
    private bool _isCheckingUpdate;

    [ObservableProperty]
    private string _updateStatusText = string.Empty;

    [ObservableProperty]
    private string? _latestVersion;

    [ObservableProperty]
    private string? _latestNotes;

    public bool HasNewVersion => LatestVersion is not null;

    [RelayCommand]
    private async Task CheckForUpdateAsync()
    {
        if (IsCheckingUpdate) return;
        IsCheckingUpdate = true;
        UpdateStatusText = Tr.Get("Settings.Update.Checking");
        LatestVersion = null;
        LatestNotes = null;
        try
        {
            using var response = await UpdateClient.GetAsync(
                "https://api.github.com/repos/huyangpahuo/Viora/releases/latest");
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await System.Text.Json.JsonDocument.ParseAsync(stream);

            var tag = doc.RootElement.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() : null;
            var notes = doc.RootElement.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() : null;

            if (string.IsNullOrEmpty(tag))
            {
                UpdateStatusText = Tr.Get("Settings.Update.Failed");
                return;
            }

            var latest = ParseVersion(tag);
            var current = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
            if (latest is null || current is null || latest <= current)
            {
                UpdateStatusText = Tr.Get("Settings.Update.UpToDate");
                return;
            }

            LatestVersion = tag;
            LatestNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            UpdateStatusText = string.Format(Tr.Get("Settings.Update.NewVersion"), tag);
        }
        catch
        {
            UpdateStatusText = Tr.Get("Settings.Update.Failed");
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    private static Version? ParseVersion(string tag)
    {
        var text = tag.TrimStart('v', 'V');
        var cut = text.IndexOf('-');
        if (cut >= 0) text = text[..cut];
        return Version.TryParse(text, out var v) ? v : null;
    }

    [RelayCommand]
    private void OpenReleasePage()
    {
        try
        {
            Process.Start(new ProcessStartInfo(ReleasesUrl) { UseShellExecute = true });
        }
        catch
        {
            // 无默认浏览器等极端情况:静默
        }
    }

    [RelayCommand]
    private void OpenRepo()
    {
        try
        {
            Process.Start(new ProcessStartInfo(RepoUrl) { UseShellExecute = true });
        }
        catch
        {
            // 无默认浏览器等极端情况:静默
        }
    }

    // ---------- 右侧:存储与空间 ----------

    public string PluginsFolder => AppLocations.PluginsFolder;

    [RelayCommand]
    private void OpenPluginsFolder() => OpenInExplorer(AppLocations.PluginsFolder);

    private static void OpenInExplorer(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch
        {
            // 打开失败不影响设置页使用
        }
    }

    // ---------- 重置 ----------

    [RelayCommand]
    private async Task ResetToDefaultsAsync()
    {
        if (!_alert.Confirm(Tr.Get("Settings.Reset.Title"), Tr.Get("Settings.Reset.Confirm"))) return;

        _settings.Update(s =>
        {
            s.General = new GeneralSettings();
            s.Appearance = new AppearanceSettings();
            s.ImageProcessing = new ImageProcessingSettings();
            s.Performance = new PerformanceSettings();
            s.Privacy = new PrivacySettings();
            s.Debug = new DebugSettings();
            // Language 与 Plugins 保持不变:语言是用户身份偏好,插件开关属于安装状态
        });

        ThemeManager.Apply(_settings.Current.Appearance.Theme);

        OnPropertyChanged(nameof(StartMaximized));
        OnPropertyChanged(nameof(RedactPathsInLogs));
        OnPropertyChanged(nameof(RedactStateText));
        OnPropertyChanged(nameof(SelectedLogLevel));
        OnPropertyChanged(nameof(DeveloperMode));
        OnPropertyChanged(nameof(UsePreviewQualityDuringInteraction));
        OnPropertyChanged(nameof(PreviewMaxDimension));
        OnPropertyChanged(nameof(ExportMaxDimension));
        foreach (var scheme in ThemeManager.Schemes) scheme.Refresh();

        _alert.Info(Tr.Get("Settings.Reset.Done"));
        await Task.CompletedTask;
    }
}

/// <summary>Task 忘等辅助(开关切换场景,异常已有内部捕获)。</summary>
file static class TaskExtensions
{
    public static void FireAndForget(this Task task) =>
        _ = task.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
}
