using Viora.Core.Pipeline;
using Viora.Core.Plugins;
using Viora.PluginSdk;
using Viora.UI.Hosting;

namespace Viora.Features.Convert.Styles;

/// <summary>
/// Wraps a single built-in style preset as an IBuiltInFeature, registering it through
/// the same IPluginContext seam external plugins use. Raster exporters are registered
/// once by AnimeVectorFeature; style features only contribute presets.
/// </summary>
public sealed class StyleFeature : IBuiltInFeature
{
    private readonly IStylePreset _preset;

    public StyleFeature(IStylePreset preset, string featureId, string displayName, string description)
    {
        _preset = preset;
        Metadata = new PluginMetadata(
            Id: featureId,
            DisplayName: displayName,
            Version: new Version(1, 0, 0),
            Author: "Viora Project",
            Description: description,
            Homepage: null,
            Repository: null,
            RequiredHostVersion: global::Viora.Core.Plugins.VersionRange.Any,
            Capabilities: new[]
            {
                Viora.Features.Convert.AnimeVector.PluginContextRegistration.PresetCapability,
            },
            Dependencies: Array.Empty<PluginDependency>());
    }

    public PluginMetadata Metadata { get; }

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken)
    {
        context.RegisterPreset(_preset);
        return Task.CompletedTask;
    }
}
