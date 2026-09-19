using Viora.Core.Plugins;

namespace Viora.Core.Settings;

public sealed class GeneralSettings
{
    public bool StartMaximized { get; set; } = true;

    /// <summary>作品库自定义目录;空 = 默认(用户数据目录\works,便携模式为 exe 旁)。重启生效。</summary>
    public string WorksFolder { get; set; } = string.Empty;

    public bool ConfirmBeforeCloseDuringProcessing { get; set; } = true;
}

public sealed class AppearanceSettings
{
    public string Theme { get; set; } = "dark";

    public double UiScale { get; set; } = 1.0;
}

public sealed class LanguageSettings
{
    public string Language { get; set; } = "en";
}

public sealed class ImageProcessingSettings
{
    public int PreviewMaxDimension { get; set; } = 1024;

    public int ExportMaxDimension { get; set; } = 4096;

    public int PaletteSize { get; set; } = 8;
}

public sealed class ExportSettings
{
    public string DefaultFormat { get; set; } = "png";

    public int PngQuality { get; set; } = 100;

    public int JpegQuality { get; set; } = 92;

    public string OutputFolder { get; set; } = string.Empty;
}

public sealed class PluginSettings
{
    public bool EnablePluginLoading { get; set; } = true;

    public List<string> DisabledPlugins { get; set; } = new();
}

public sealed class PerformanceSettings
{
    public int MaxDegreeOfParallelism { get; set; } = 0; // 0 = auto

    public bool UsePreviewQualityDuringInteraction { get; set; } = true;
}

public sealed class CacheSettings
{
    public bool EnableCache { get; set; } = true;

    public string CachePath { get; set; } = string.Empty; // empty = default under app data

    public long MaxCacheSizeMb { get; set; } = 512;
}

public sealed class PrivacySettings
{
    public bool ShareAnonymousUsage { get; set; } = false;

    public bool RedactPathsInLogs { get; set; } = true;
}

public sealed class DebugSettings
{
    public bool DeveloperMode { get; set; } = false;

    /// <summary>日志目录自定义;空 = 默认。重启生效。</summary>
    public string LogFolder { get; set; } = string.Empty;

    public string LogLevel { get; set; } = "Information";
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

    public PerformanceSettings Performance { get; set; } = new();

    public CacheSettings Cache { get; set; } = new();

    public PrivacySettings Privacy { get; set; } = new();

    public DebugSettings Debug { get; set; } = new();
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
