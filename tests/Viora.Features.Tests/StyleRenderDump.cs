using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Styles;
using Viora.Infrastructure.Pipeline;
using Xunit;

namespace Viora.Features.Tests;

/// <summary>
/// Manual visual-check helper: renders the recently reworked styles to PNG files under
/// artifacts/style-previews so a human (or the agent) can eyeball the result.
/// Run with: dotnet test --filter FullyQualifiedName~StyleRenderDump
/// </summary>
public class StyleRenderDump
{
    [Fact]
    public async Task Render_Fixed_Styles()
    {
        string dir = @"D:\Viora\artifacts\style-previews";
        Directory.CreateDirectory(dir);

        var engine = new ImageConversionEngine();
        string[] targets =
        {
            "builtin.stippling", "builtin.cross-hatching", "builtin.etching", "builtin.fresco",
            "builtin.mosaic-glass", "builtin.metal-engraving", "builtin.neon-sign", "builtin.liquid-metal",
            "builtin.chrome", "builtin.glowing-wireframe", "builtin.torn-paper", "builtin.tape-art",
            "builtin.string-art", "builtin.sand-art", "builtin.smoke-art", "builtin.light-painting",
            "builtin.kaleidoscope", "builtin.liquid-marble", "builtin.dithered", "builtin.globe-relief",
            "builtin.low-poly", "builtin.clay", "builtin.halftone", "builtin.collage",
            "builtin.porcelain", "builtin.origami", "builtin.isometric-diorama", "builtin.neon-cyberpunk",
        };

        foreach (var (preset, _, _, _) in BuiltInStyles.All)
        {
            if (!targets.Contains(preset.Id)) continue;
            var buffer = Scene(192, 144);
            var result = await engine.ExecuteAsync(
                preset.BuildPipeline(new Dictionary<string, object>()),
                buffer, new Dictionary<string, object>(), true, null, CancellationToken.None);
            SavePng(result.Result, Path.Combine(dir, preset.Id["builtin.".Length..] + ".png"));
        }
    }

    /// <summary>Synthetic scene: sky gradient, sun, hills, a house — edges, flats and gradients.</summary>
    private static RgbaImageBuffer Scene(int w, int h)
    {
        var buf = new RgbaImageBuffer(w, h);
        var px = buf.Pixels;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * buf.Stride + x * 4;
                byte b, g, r;
                if (y < h * 0.55)
                {
                    double t = y / (h * 0.55);
                    b = (byte)(250 - t * 60); g = (byte)(190 + t * 40); r = (byte)(120 + t * 90);
                }
                else
                {
                    double t = (y - h * 0.55) / (h * 0.45);
                    b = (byte)(90 - t * 30); g = (byte)(150 - t * 40); r = (byte)(70 + t * 20);
                }

                // sun
                double dx = x - w * 0.72, dy = y - h * 0.24;
                if (dx * dx + dy * dy < 20 * 20) { b = 90; g = 190; r = 250; }

                // hills silhouette
                double hill = h * 0.55 - 26 * Math.Sin(x * 0.05) - 14 * Math.Sin(x * 0.13 + 2);
                if (y > hill) { b = 60; g = 110; r = 55; }

                // house
                if (x > w * 0.18 && x < w * 0.34 && y > h * 0.42 && y < h * 0.62) { b = 140; g = 160; r = 190; }
                if (x > w * 0.16 && x < w * 0.36 && y > h * 0.34 && y < h * 0.44 &&
                    Math.Abs(x - w * 0.26) < (y - h * 0.30)) { b = 60; g = 60; r = 150; }

                px[i] = b; px[i + 1] = g; px[i + 2] = r; px[i + 3] = 255;
            }
        }
        return buf;
    }

    private static void SavePng(IImageBuffer buffer, string path)
    {
        int size = buffer.Stride * buffer.Height;
        var ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(buffer.Pixels, 0, ptr, size);
            var source = BitmapSource.Create(buffer.Width, buffer.Height, 96, 96, PixelFormats.Bgra32, null,
                ptr, size, buffer.Stride);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var fs = File.Create(path);
            encoder.Save(fs);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
        }
    }
}
