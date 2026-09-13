using Viora.Core.Imaging;
using Viora.Core.Pipeline;

namespace Viora.Core.Pipeline;

public sealed record ConversionResult(
    IImageBuffer Result,
    TimeSpan Duration,
    bool CompletedAsPreview);

public sealed record PipelineProgress(
    double OverallFraction,
    StageProgress? CurrentStage,
    string? Message);

/// <summary>
/// Executes an ordered stage list asynchronously with progress + cancellation.
/// The engine never assumes a fixed stage list.
/// </summary>
public interface IImageConversionEngine
{
    Task<ConversionResult> ExecuteAsync(
        IReadOnlyList<IImageProcessingStage> pipeline,
        IImageBuffer source,
        IReadOnlyDictionary<string, object> parameters,
        bool previewQuality,
        IProgress<PipelineProgress>? progress,
        CancellationToken cancellationToken);
}
