using Viora.Core.Plugins;

namespace Viora.PluginSdk;

/// <summary>
/// Entry point every Viora plugin implements. One instance per plugin;
/// the host constructs it via its parameterless constructor or a single
/// constructor accepting IPluginContext-compatible services.
/// </summary>
public interface IVioraPlugin
{
    PluginMetadata Metadata { get; }

    Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken);

    Task ShutdownAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Optional convenience base implementing Metadata from constructor args.
/// </summary>
public abstract class VioraPluginBase : IVioraPlugin
{
    protected VioraPluginBase(PluginMetadata metadata) => Metadata = metadata;

    public PluginMetadata Metadata { get; }

    public abstract Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken);

    public virtual Task ShutdownAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
