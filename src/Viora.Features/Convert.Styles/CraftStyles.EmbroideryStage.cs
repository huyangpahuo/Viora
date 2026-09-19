using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


public sealed class EmbroideryStage : StageBase
{
    public override string Name => "Embroidery";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        int stitch = Math.Clamp(Int(context.Parameters, "stitch", 6), 3, 24);
        int colors = Math.Clamp(Int(context.Parameters, "colors", 8), 2, 24);

        var palette = ImageOps.ExtractPalette(src, colors);
        var original = (byte[])px.Clone();

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                var p = palette[ImageOps.NearestPaletteIndex(palette, original[i], original[i + 1], original[i + 2])];

                // thread sheen: per-stitch cell diagonal running bond
                int cellX = x / stitch, cellY = y / stitch;
                double angle = ((cellX + cellY) & 1) == 0 ? Math.PI / 4 : -Math.PI / 4;
                double along = Math.Cos(angle) * (x % stitch) + Math.Sin(angle) * (y % stitch);
                double sheen = 0.78 + 0.42 * (0.5 + 0.5 * Math.Sin(along * (Math.PI / Math.Max(2, stitch / 2.0))));

                // fabric weave between stitches
                double weave = 1.0 + (ImageOps.Hash(x, y, 19) - 0.5) * 0.10;

                px[i] = ImageOps.Clamp((int)(p.B * sheen * weave));
                px[i + 1] = ImageOps.Clamp((int)(p.G * sheen * weave));
                px[i + 2] = ImageOps.Clamp((int)(p.R * sheen * weave));
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

