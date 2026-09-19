using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


public sealed class WaxCrayonStage : StageBase
{
    public override string Name => "WaxCrayon";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        int stroke = Math.Clamp(Int(context.Parameters, "stroke", 8), 3, 24);
        double pressure = Dbl(context.Parameters, "pressure", 0.6);

        var palette = ImageOps.ExtractPalette(src, 6);
        var original = (byte[])px.Clone();

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                var p = palette[ImageOps.NearestPaletteIndex(palette, original[i], original[i + 1], original[i + 2])];

                // wax skip: coverage from stroke-direction streaks + grain
                double streak = 0.5 + 0.5 * Math.Sin((y + (x / (double)stroke) * stroke * 0.7) * 2.2);
                double grain = ImageOps.Hash(x, y, 61);
                double coverage = Math.Clamp(pressure * (0.35 + 0.5 * streak) + (grain - 0.5) * 0.5, 0, 1);

                int paperB = 248, paperG = 245, paperR = 238;
                px[i] = ImageOps.Clamp((int)(paperB * (1 - coverage) + p.B * coverage));
                px[i + 1] = ImageOps.Clamp((int)(paperG * (1 - coverage) + p.G * coverage));
                px[i + 2] = ImageOps.Clamp((int)(paperR * (1 - coverage) + p.R * coverage));
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

