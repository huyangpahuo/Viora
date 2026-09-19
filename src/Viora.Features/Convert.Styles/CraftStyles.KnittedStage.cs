using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


public sealed class KnittedStage : StageBase
{
    public override string Name => "Knitted";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        int size = Math.Clamp(Int(context.Parameters, "stitchSize", 10), 4, 32);
        int colors = Math.Clamp(Int(context.Parameters, "colors", 6), 2, 16);

        var palette = ImageOps.ExtractPalette(src, colors);
        var original = (byte[])px.Clone();

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                var p = palette[ImageOps.NearestPaletteIndex(palette, original[i], original[i + 1], original[i + 2])];

                // one knit "V" per cell: two crossed lobes
                double lx = (x % (double)size) / size - 0.5;
                double ly = (y % (double)size) / (double)size;
                double lobe = Math.Exp(-Math.Pow(Math.Abs(lx) * 2 - ly * 0.8, 2) * 5.0);
                double shade = 0.62 + 0.55 * Math.Clamp(lobe, 0, 1);

                // darker inter-loop shadow
                if (ly < 0.14) shade *= 0.55;

                double fuzz = 1.0 + (ImageOps.Hash(x, y, 23) - 0.5) * 0.12;
                px[i] = ImageOps.Clamp((int)(p.B * shade * fuzz));
                px[i + 1] = ImageOps.Clamp((int)(p.G * shade * fuzz));
                px[i + 2] = ImageOps.Clamp((int)(p.R * shade * fuzz));
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

