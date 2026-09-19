using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


public sealed class PixelArtStage : StageBase
{
    public override string Name => "PixelArt";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        int tile = Math.Clamp(Int(context.Parameters, "pixelSize", 8), 2, 64);
        int colors = Math.Clamp(Int(context.Parameters, "colors", 16), 2, 48);

        var px = src.Pixels;
        var palette = ImageOps.ExtractPalette(src, colors);

        var dst = new RgbaImageBuffer(w, h);
        ImageOps.OpaqueAlpha(dst.Pixels);
        var outPx = dst.Pixels;
        int cols = (w + tile - 1) / tile, rows = (h + tile - 1) / tile;
        var cellColor = new int[cols * rows];

        // tile average
        for (int ty = 0; ty < rows; ty++)
            for (int tx = 0; tx < cols; tx++)
            {
                long sb = 0, sg = 0, sr = 0; int n = 0;
                int x1 = Math.Min(w, (tx + 1) * tile), y1 = Math.Min(h, (ty + 1) * tile);
                for (int y = ty * tile; y < y1; y++)
                    for (int x = tx * tile; x < x1; x++)
                    {
                        int i = y * stride + x * 4;
                        sb += px[i]; sg += px[i + 1]; sr += px[i + 2]; n++;
                    }
                cellColor[ty * cols + tx] = ImageOps.NearestPaletteIndex(palette,
                    (int)(sb / n), (int)(sg / n), (int)(sr / n));
            }

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = palette[cellColor[(y / tile) * cols + x / tile]];
                int i = y * dst.Stride + x * 4;
                outPx[i] = (byte)p.B; outPx[i + 1] = (byte)p.G; outPx[i + 2] = (byte)p.R; outPx[i + 3] = 255;
            }
        progress?.Report(new StageProgress(Name, 0, 1, 1.0));

        context.Working = dst;
        return Task.FromResult(context);
    }
}

