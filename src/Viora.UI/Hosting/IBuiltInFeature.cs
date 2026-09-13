using Viora.Core.Plugins;

namespace Viora.UI.Hosting;

/// <summary>
/// A built-in feature. Implemented by Viora.Features assemblies and registered
/// via IFeatureRegistry through the identical IPluginContext seam plugins use.
/// </summary>
public interface IBuiltInFeature
{
    PluginMetadata Metadata { get; }

    Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken);
}
