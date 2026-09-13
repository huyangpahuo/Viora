using Viora.Core.Pipeline;

namespace Viora.Features.Convert.Common;

/// <summary>Parameter keys shared by conversion presets (single source of truth for VM + stages).</summary>
public static class ParamKeys
{
    public const string Colors = "colors";
    public const string Smoothing = "smoothing";
    public const string Detail = "detail";
    public const string Edges = "edges";
    public const string Shadows = "shadows";
    public const string Saturation = "saturation";
}

/// <summary>Base class handling parameter extraction with defaults.</summary>
public abstract class StageBase : IImageProcessingStage
{
    public abstract string Name { get; }

    public abstract Task<ImageProcessingContext> ExecuteAsync(
        ImageProcessingContext context,
        IProgress<StageProgress>? progress,
        CancellationToken cancellationToken);

    protected static int Int(IReadOnlyDictionary<string, object> p, string key, int fallback) =>
        p.TryGetValue(key, out var v) && v is IConvertible c ? System.Convert.ToInt32(c, System.Globalization.CultureInfo.InvariantCulture) : fallback;

    protected static double Dbl(IReadOnlyDictionary<string, object> p, string key, double fallback) =>
        p.TryGetValue(key, out var v) && v is IConvertible c ? System.Convert.ToDouble(c, System.Globalization.CultureInfo.InvariantCulture) : fallback;

    protected static bool Bool(IReadOnlyDictionary<string, object> p, string key, bool fallback) =>
        p.TryGetValue(key, out var v) && v is bool b ? b : fallback;
}
