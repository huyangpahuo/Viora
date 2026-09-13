using Viora.Core.Pipeline;
using Viora.Core.Plugins;
using Viora.PluginSdk;

namespace SamplePlugin;

/// <summary>
/// Minimal Viora plugin proving the full host lifecycle: install → enable →
/// contribute a preset → disable → uninstall. Ships a "Duotone" stylization preset.
/// </summary>
public sealed class SampleVioraPlugin : VioraPluginBase
{
    public SampleVioraPlugin()
        : base(new PluginMetadata(
            Id: "com.viora.sample-duotone",
            DisplayName: "Duotone (Sample)",
            Version: new Version(1, 0, 0),
            Author: "Viora Project",
            Description: "Sample plugin: duotone stylization preset proving the plugin lifecycle.",
            Homepage: new Uri("https://github.com/viora-project/viora"),
            Repository: new Uri("https://github.com/viora-project/viora"),
            RequiredHostVersion: VersionRange.Parse(">=1.0.0"),
            Capabilities: new[] { "convert.preset", "localization.strings" },
            Dependencies: Array.Empty<PluginDependency>()))
    {
    }

    public override Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken)
    {
        context.RegisterPreset(new DuotonePreset());
        // Convention: "lang::Full.Key" → text. The host injects these into the
        // localization service so the Convert page can bind them like any other string.
        context.RegisterStrings(new Dictionary<string, string>
        {
            ["en::Preset.Duotone.Name"] = "Duotone",
            ["en::Preset.Duotone.Description"] = "Two-tone poster stylization from the sample plugin.",
            ["zh-Hans::Preset.Duotone.Name"] = "双色调",
            ["zh-Hans::Preset.Duotone.Description"] = "示例插件提供的双色调海报风格化。",
        });
        return Task.CompletedTask;
    }
}

/// <summary>Two-color posterize: quantize to a shadow + highlight tone around the median luma.</summary>
public sealed class DuotonePreset : IStylePreset
{
    public string Id => "plugin.sample.duotone";

    public string DisplayNameKey => "Preset.Duotone.Name";

    public string DescriptionKey => "Preset.Duotone.Description";

    public string? IconGlyph => "\uE790";

    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("strength", "Param.Shadows", 0.8, 0.0, 1.0, 0.05),
    };

    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> parameters)
        => new IImageProcessingStage[] { new DuotoneStage() };
}

public sealed class DuotoneStage : IImageProcessingStage
{
    public string Name => "Duotone";

    public Task<ImageProcessingContext> ExecuteAsync(
        ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken cancellationToken)
    {
        var src = context.Working!;
        var px = src.Pixels;
        for (int i = 0; i < px.Length; i += 4)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int luma = (px[i + 2] * 299 + px[i + 1] * 587 + px[i] * 114) / 1000;
            byte shadow = (byte)(30 + luma / 8);
            byte light = (byte)(90 + luma / 3);
            px[i] = shadow; px[i + 1] = (byte)((shadow + light) / 2); px[i + 2] = light;
        }
        progress?.Report(new StageProgress(Name, 0, 1, 1.0));
        return Task.FromResult(context);
    }
}
