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
public sealed record LanguageOption(string Code, string DisplayName);

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly ILocalizationService _localization;
    private readonly IPluginHost _pluginHost;
    private readonly IWorksStore _works;
    private readonly IUiAlert _alert;
    private readonly ILogger<SettingsViewModel> _logger;
    private bool _pluginRowsLoaded;

    public SettingsViewModel(
        ISettingsService settings,
        ILocalizationService localization,
        IPluginHost pluginHost,
        IWorksStore works,
        IUiAlert alert,
        ILogger<SettingsViewModel> logger)
    {
        _settings = settings;
        _localization = localization;
        _pluginHost = pluginHost;
        _works = works;
        _alert = alert;
        _logger = logger;

        Sections = new ObservableCollection<SettingsSectionViewModel>
        {
            new("general", "Settings.General", "Settings.General.Sub", "Gear"),
            new("appearance", "Settings.Appearance", "Settings.Appearance.Sub", "Image"),
            new("performance", "Settings.Performance", "Settings.Performance.Sub", "Sliders"),
            new("plugins", "Settings.Plugins", "Settings.Plugins.Sub", "PuzzlePiece"),
            new("ai", "Settings.AI", "Settings.AI.Sub", "WandMagicSparkles", isEnabled: false),
            new("hotkeys", "Settings.Hotkeys", "Settings.Hotkeys.Sub", "List", isEnabled: false),
            new("about", "Settings.About", "Settings.About.Sub", "CircleInfo"),
        };

        LanguageItems = _localization.AvailableLanguages
            .Select(code => new LanguageOption(code, code.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "中文(简体)" : "English"))
            .ToList();
        LogLevels = ["Trace", "Debug", "Information", "Warning", "Error"];

        SelectedSection = Sections[0];
    }

    // ---------- 子导航 ----------

    public ObservableCollection<SettingsSectionViewModel> Sections { get; }

    [ObservableProperty]
    private SettingsSectionViewModel _selectedSection;

    partial void OnSelectedSectionChanged(SettingsSectionViewModel? value)
    {
        OnPropertyChanged(nameof(IsGeneralSelected));
        OnPropertyChanged(nameof(IsAppearanceSelected));
        OnPropertyChanged(nameof(IsPerformanceSelected));
        OnPropertyChanged(nameof(IsPluginsSelected));
        OnPropertyChanged(nameof(IsAboutSelected));
        if (value?.Key == "plugins" && !_pluginRowsLoaded)
            _ = LoadPluginRowsAsync();
    }

    public bool IsGeneralSelected => SelectedSection?.Key == "general";

    public bool IsAppearanceSelected => SelectedSection?.Key == "appearance";

    public bool IsPerformanceSelected => SelectedSection?.Key == "performance";

    public bool IsPluginsSelected => SelectedSection?.Key == "plugins";

    public bool IsAboutSelected => SelectedSection?.Key == "about";

    // ---------- 通用 ----------

    public bool StartMaximized
    {
        get => _settings.Current.General.StartMaximized;
        set => _settings.Update(s => s.General.StartMaximized = value);
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
        }
    }

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

    public IReadOnlyList<ThemeScheme> Themes => ThemeManager.Schemes;

    [RelayCommand]
    private void ApplyTheme(string? schemeId)
    {
        if (string.IsNullOrEmpty(schemeId)) return;
        ThemeManager.Apply(schemeId);
        _settings.Update(s => s.Appearance.Theme = schemeId);
        foreach (var scheme in ThemeManager.Schemes) scheme.Refresh();
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

    public string WorksFolder => _works.WorksFolder;

    public string PluginsFolder => AppLocations.PluginsFolder;

    [RelayCommand]
    private void OpenWorksFolder() => OpenInExplorer(_works.WorksFolder);

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
