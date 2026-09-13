using Viora.Core.Pipeline;
using Viora.Core.Imaging;
using Viora.Features.Convert.Styles;
using Viora.Infrastructure.Pipeline;
using Xunit;

namespace Viora.Features.Tests;

/// <summary>
/// Built-in style preset smoke tests: every stage of every style pipeline must run to
/// completion on synthetic input, keep buffer dimensions, and actually change pixels.
/// Deterministic inputs; no golden pixels.
/// </summary>
public class BuiltInStylePipelineTests
{
    public static TheoryData<IStylePreset> AllStyles
    {
        get
        {
            var data = new TheoryData<IStylePreset>();
            foreach (var (preset, _, _, _) in BuiltInStyles.All)
                data.Add(preset);
            return data;
        }
    }

    private static RgbaImageBuffer MakeTestImage(int w = 48, int h = 36)
    {
        var buffer = new RgbaImageBuffer(w, h);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * buffer.Stride + x * 4;
                buffer.Pixels[i] = (byte)(x * 5);        // B ramp
                buffer.Pixels[i + 1] = (byte)(y * 6);    // G ramp
                buffer.Pixels[i + 2] = (byte)(x < w / 2 ? 10 : 250); // hard vertical edge
                buffer.Pixels[i + 3] = 255;
            }
        }
        return buffer;
    }

    [Theory]
    [MemberData(nameof(AllStyles))]
    public async Task Pipeline_Runs_AllStages_And_ChangesPixels(IStylePreset preset)
    {
        var source = MakeTestImage();
        var engine = new ImageConversionEngine();
        var result = await engine.ExecuteAsync(
            preset.BuildPipeline(new Dictionary<string, object>()),
            source,
            new Dictionary<string, object>(),
            previewQuality: true,
            progress: null,
            cancellationToken: CancellationToken.None);

        Assert.Equal(source.Width, result.Result.Width);
        Assert.Equal(source.Height, result.Result.Height);

        // The stylization must visibly alter the input.
        bool changed = false;
        for (int i = 0; i < source.Pixels.Length; i += 4)
        {
            if (Math.Abs(result.Result.Pixels[i] - source.Pixels[i]) > 2 ||
                Math.Abs(result.Result.Pixels[i + 2] - source.Pixels[i + 2]) > 2)
            {
                changed = true;
                break;
            }
        }
        Assert.True(changed, $"{preset.Id} pipeline left the image unchanged.");
    }

    [Theory]
    [MemberData(nameof(AllStyles))]
    public async Task Pipeline_Honors_Cancellation(IStylePreset preset)
    {
        var source = MakeTestImage();
        var engine = new ImageConversionEngine();
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // cancelled before the first stage runs

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            engine.ExecuteAsync(
                preset.BuildPipeline(new Dictionary<string, object>()),
                source,
                new Dictionary<string, object>(),
                previewQuality: true,
                progress: null,
                cancellationToken: cts.Token));
    }

    [Fact]
    public async Task Mosaic_HighGrout_DarkensSeams()
    {
        var source = MakeTestImage();
        var engine = new ImageConversionEngine();
        var parameters = new Dictionary<string, object> { ["tileSize"] = 8, ["grout"] = 1.0 };
        var result = await engine.ExecuteAsync(
            new MosaicPreset().BuildPipeline(parameters), source, parameters, true, null, CancellationToken.None);

        // At grout=1.0 the seam center is pulled fully to the grout color (52 in red),
        // while a mid-tile pixel (4, 20 — dx/dy ≥ 3 from seams with tile size 8)
        // keeps its tile color. Direction-agnostic: works for dark and bright tiles alike.
        int seam = GetPixel(result.Result, 0, 16);
        int center = GetPixel(result.Result, 4, 20);
        Assert.True(Math.Abs(seam - 52) <= 4, $"seam should be grout-colored, got {seam}.");
        Assert.True(Math.Abs(center - 52) > 8, $"tile interior should keep tile color, got {center}.");
    }

    [Fact]
    public async Task Sketch_ProducesNearWhitePaper_WithDarkLines()
    {
        var source = MakeTestImage();
        var engine = new ImageConversionEngine();
        var parameters = new Dictionary<string, object> { ["lineThreshold"] = 0.9, ["shading"] = 0.0 };
        var result = await engine.ExecuteAsync(
            new SketchPreset().BuildPipeline(parameters), source, parameters, true, null, CancellationToken.None);

        // Flat interior (far from the vertical edge) must stay paper-bright.
        int interior = GetPixel(result.Result, 8, 4);
        Assert.True(interior > 200, $"paper interior should be bright, got {interior}.");

        // Somewhere near the hard edge there must be dark ink.
        int minLuma = 255;
        for (int y = 0; y < result.Result.Height; y++)
        {
            for (int x = result.Result.Width / 2 - 2; x <= result.Result.Width / 2 + 2; x++)
            {
                int i = y * result.Result.Stride + x * 4;
                int luma = (result.Result.Pixels[i + 2] * 299 + result.Result.Pixels[i + 1] * 587 + result.Result.Pixels[i] * 114) / 1000;
                minLuma = Math.Min(minLuma, luma);
            }
        }
        Assert.True(minLuma < 120, $"edge ink should be dark, got min luma {minLuma}.");
    }

    private static int GetPixel(IImageBuffer buffer, int x, int y) =>
        buffer.Pixels[y * buffer.Stride + x * 4 + 2]; // red channel
}
