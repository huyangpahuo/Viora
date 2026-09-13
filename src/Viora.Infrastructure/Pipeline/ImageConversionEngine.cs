using Viora.Core.Imaging;
using Viora.Core.Pipeline;

namespace Viora.Infrastructure.Pipeline;

/// <summary>
/// Executes ordered stages with per-stage progress and cancellation.
/// Runs stages sequentially on the thread pool; stages themselves parallelize
/// pixel loops internally where useful.
/// </summary>
public sealed class ImageConversionEngine : IImageConversionEngine
{
    public async Task<ConversionResult> ExecuteAsync(
        IReadOnlyList<IImageProcessingStage> pipeline,
        IImageBuffer source,
        IReadOnlyDictionary<string, object> parameters,
        bool previewQuality,
        IProgress<PipelineProgress>? progress,
        CancellationToken cancellationToken)
    {
        var started = DateTime.UtcNow;
        var context = new ImageProcessingContext { Source = source, Parameters = parameters, Working = source.Clone() };

        int total = pipeline.Count;
        for (int i = 0; i < total; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stage = pipeline[i];
            double baseFraction = (double)i / total;

            var stageProgress = new ProgressMapper(progress, stage.Name, i, total, baseFraction, 1.0 / total);
            context = await stage.ExecuteAsync(context, stageProgress, cancellationToken).ConfigureAwait(false);
        }

        progress?.Report(new PipelineProgress(1.0, null, null));
        return new ConversionResult(context.Working ?? context.Source, DateTime.UtcNow - started, previewQuality);
    }

    /// <summary>Maps a stage's 0..1 fraction onto overall pipeline progress.</summary>
    private sealed class ProgressMapper : IProgress<StageProgress>
    {
        private readonly IProgress<PipelineProgress>? _sink;
        private readonly string _name;
        private readonly int _index;
        private readonly int _count;
        private readonly double _baseFraction;
        private readonly double _span;

        public ProgressMapper(IProgress<PipelineProgress>? sink, string name, int index, int count, double baseFraction, double span)
        {
            _sink = sink; _name = name; _index = index; _count = count; _baseFraction = baseFraction; _span = span;
        }

        public void Report(StageProgress value)
        {
            double fraction = Math.Clamp(value.StageFraction, 0, 1);
            _sink?.Report(new PipelineProgress(
                _baseFraction + fraction * _span,
                new StageProgress(_name, _index, _count, fraction),
                null));
        }
    }
}
