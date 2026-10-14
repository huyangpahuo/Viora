using Viora.Core.Plugins;

namespace Viora.Core.Settings;

public sealed class GeneralSettings
{
    public bool StartMaximized { get; set; } = true;

    /// <summary>作品库自定义目录;空 = 默认(用户数据目录\works,便携模式为 exe 旁)。重启生效。</summary>
    public string WorksFolder { get; set; } = string.Empty;

    /// <summary>单张导入图片大小上限(MB);0 = 不限制。</summary>
    public int ImportMaxSizeMb { get; set; } = 10;

    /// <summary>界面字体(本机字体族名);空 = 默认。</summary>
    public string FontFamily { get; set; } = string.Empty;
}

public sealed class AppearanceSettings
{
    public string Theme { get; set; } = "sunset";

    public double UiScale { get; set; } = 1.0;

    // 插件工坊编辑器
    public double WorkshopFontSize { get; set; } = 12.5;

    public bool WorkshopJetBrainsHighlight { get; set; } = true;

    public bool WorkshopShowMinimap { get; set; } = true;

    public string ExternalEditorPath { get; set; } = string.Empty;
}

public sealed class LanguageSettings
{
    public string Language { get; set; } = "en";
}

public sealed class ImageProcessingSettings
{
    public int PreviewMaxDimension { get; set; } = 1024;

    public int ExportMaxDimension { get; set; } = 4096;
}

public sealed class ExportSettings
{
    public string DefaultFormat { get; set; } = "png";

    public int JpegQuality { get; set; } = 92;
}

public sealed class HotkeySettings
{
    /// <summary>快捷键 id → 组合键串(如 stylize.run → Ctrl+Enter);空串 = 未设置。</summary>
    public Dictionary<string, string> Bindings { get; set; } = new();
}

public sealed class PluginSettings
{
    public bool EnablePluginLoading { get; set; } = true;

    public List<string> DisabledPlugins { get; set; } = new();
}

/// <summary>性能设置。</summary>
public sealed class PerformanceSettings
{
    public bool UsePreviewQualityDuringInteraction { get; set; } = true;
}

/// <summary>隐私设置。</summary>
public sealed class PrivacySettings
{
    public bool RedactPathsInLogs { get; set; } = true;
}

public sealed class DebugSettings
{
    public bool DeveloperMode { get; set; } = false;

    /// <summary>日志目录自定义;空 = 默认。重启生效。</summary>
    public string LogFolder { get; set; } = string.Empty;

    public string LogLevel { get; set; } = "Information";
}

/// <summary>插件工坊(独立应用,重新设计中)的设置;主程序不读写此节。</summary>
public sealed class WorkshopSettings
{
    /// <summary>工作区文件夹(插件的源码就住在这里,自动保存);空 = 默认(文档\Viora Workshop\MyStyle)。</summary>
    public string WorkspaceFolder { get; set; } = string.Empty;
}

/// <summary>The full settings document, versioned for future migrations.</summary>
public sealed class VioraSettings
{
    public int SchemaVersion { get; set; } = 1;

    public GeneralSettings General { get; set; } = new();

    public AppearanceSettings Appearance { get; set; } = new();

    public LanguageSettings Language { get; set; } = new();

    public ImageProcessingSettings ImageProcessing { get; set; } = new();

    public ExportSettings Export { get; set; } = new();

    public PluginSettings Plugins { get; set; } = new();

    public HotkeySettings Hotkeys { get; set; } = new();

    public PerformanceSettings Performance { get; set; } = new();

    public PrivacySettings Privacy { get; set; } = new();

    public DebugSettings Debug { get; set; } = new();

    public WorkshopSettings Workshop { get; set; } = new();
}

public interface ISettingsService
{
    VioraSettings Current { get; }

    event EventHandler? SettingsChanged;

    /// <summary>Loads settings from disk (or defaults). Returns the loaded document.</summary>
    Task<VioraSettings> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists the current document (debounced by the implementation when interactive).</summary>
    Task SaveAsync(CancellationToken cancellationToken = default);

    void Update(Action<VioraSettings> mutate);
}
