using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Viora.Core.Plugins;

namespace Viora.UI.Hosting;

/// <summary>
/// Registers built-in features through the same IPluginContext seam used by
/// external plugins, so the Anime Vector preset exercises the extensibility path.
/// </summary>
public interface IFeatureRegistry
{
    Task RegisterBuiltInsAsync(CancellationToken cancellationToken = default);
}

public sealed class FeatureRegistry : IFeatureRegistry
{
    private readonly IPluginContext _pluginContext;
    private readonly IEnumerable<IBuiltInFeature> _builtIns;
    private readonly ILogger<FeatureRegistry> _logger;

    public FeatureRegistry(
        IPluginContext pluginContext,
        IEnumerable<IBuiltInFeature> builtIns,
        ILogger<FeatureRegistry> logger)
    {
        _pluginContext = pluginContext;
        _builtIns = builtIns;
        _logger = logger;
    }

    public async Task RegisterBuiltInsAsync(CancellationToken cancellationToken = default)
    {
        foreach (var feature in _builtIns)
        {
            try
            {
                await feature.InitializeAsync(_pluginContext, cancellationToken);
                _logger.LogInformation("Built-in feature {FeatureId} registered", feature.Metadata.Id);
            }
#pragma warning disable CA1031 // Host boundary: a failing built-in must not crash startup.
            catch (Exception ex)
            {
                _logger.LogError(ex, "Built-in feature {FeatureId} failed to register", feature.Metadata.Id);
            }
#pragma warning restore CA1031
        }
    }
}
