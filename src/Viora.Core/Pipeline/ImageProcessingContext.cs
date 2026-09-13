using Viora.Core.Imaging;

namespace Viora.Core.Pipeline;

/// <summary>Mutable state handed through an ordered pipeline of stages.</summary>
public sealed class ImageProcessingContext
{
    public required IImageBuffer Source { get; init; }

    public IImageBuffer? Working { get; set; }

    public IReadOnlyDictionary<string, object> Parameters { get; init; } =
        new Dictionary<string, object>();

    /// <summary>Populated by segmentation stages; downstream stages may consume it.</summary>
    public IList<IShapeRegion> Regions { get; } = new List<IShapeRegion>();

    /// <summary>Free-form shared state stages read/write (e.g. the quantized palette).</summary>
    public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>();
}

/// <summary>A closed shape region produced by segmentation/consolidation stages.</summary>
public interface IShapeRegion
{
    int Id { get; }

    /// <summary>Average color of the region as BGRA bytes.</summary>
    (byte B, byte G, byte R, byte A) Color { get; }

    int PixelCount { get; }

    /// <summary>Simplified polygon boundary in image pixel coordinates.</summary>
    IReadOnlyList<(double X, double Y)> Boundary { get; }
}

public sealed record StageProgress(
    string StageName,
    int StageIndex,
    int StageCount,
    double StageFraction);

public interface IImageProcessingStage
{
    string Name { get; }

    Task<ImageProcessingContext> ExecuteAsync(
        ImageProcessingContext context,
        IProgress<StageProgress>? progress,
        CancellationToken cancellationToken);
}
