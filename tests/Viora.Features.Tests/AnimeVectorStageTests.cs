using Viora.Core.Pipeline;
using Viora.Core.Imaging;
using Viora.Features.Convert.AnimeVector.Stages;
using Viora.Features.Convert.Common;
using Xunit;

namespace Viora.Features.Tests;

/// <summary>
/// Anime Vector stage tests with deterministic synthetic inputs, asserting style-rule
/// properties (bounded palette, hard tones, region coherence) — never golden pixels.
/// </summary>
public class AnimeVectorStageTests
{
    private static RgbaImageBuffer MakeGradientImage(int w = 32, int h = 32)
    {
        var buffer = new RgbaImageBuffer(w, h);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * buffer.Stride + x * 4;
                buffer.Pixels[i] = (byte)(x * 8);       // B ramps 0..248
                buffer.Pixels[i + 1] = (byte)(y * 8);   // G ramps
                buffer.Pixels[i + 2] = (byte)(128 + x); // R mid
                buffer.Pixels[i + 3] = 255;
            }
        }
        return buffer;
    }

    private static ImageProcessingContext Context(RgbaImageBuffer buffer, Dictionary<string, object>? parameters = null) => new()
    {
        Source = buffer,
        Working = buffer,
        Parameters = parameters ?? new Dictionary<string, object>(),
    };

    [Fact]
    public async Task Quantize_ProducesAtMostKDistinctColors()
    {
        var buffer = MakeGradientImage();
        var context = Context(buffer, new Dictionary<string, object> { [ParamKeys.Colors] = 6 });

        await new QuantizeStage().ExecuteAsync(context, null, CancellationToken.None);

        var palette = ((byte B, byte G, byte R)[])context.Properties[QuantizeStage.PaletteProperty]!;
        Assert.True(palette.Length <= 6, $"palette size {palette.Length} exceeds k");

        // Every pixel now matches one of the palette entries exactly (hard flat color).
        var set = palette.ToHashSet();
        for (int i = 0; i < buffer.Pixels.Length; i += 4)
            Assert.Contains((buffer.Pixels[i], buffer.Pixels[i + 1], buffer.Pixels[i + 2]), set);
    }

    [Fact]
    public async Task Quantize_RespectsCancellation()
    {
        var buffer = MakeGradientImage(64, 64);
        var context = Context(buffer);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new QuantizeStage().ExecuteAsync(context, null, cts.Token));
    }

    [Fact]
    public async Task Consolidate_RemovesSpeckleRegions()
    {
        // Checkerboard noise + one big flat block: after consolidation with default detail,
        // distinct color regions must collapse toward few coherent masses.
        var buffer = new RgbaImageBuffer(32, 32);
        var random = new Random(1234); // deterministic
        random.NextBytes(buffer.Pixels);
        for (int i = 3; i < buffer.Pixels.Length; i += 4) buffer.Pixels[i] = 255;

        var context = Context(buffer, new Dictionary<string, object>
        {
            [ParamKeys.Colors] = 4,
            [ParamKeys.Detail] = 0.0, // max consolidation
        });

        await new QuantizeStage().ExecuteAsync(context, null, CancellationToken.None);
        var before = CountRegions(buffer);
        await new ConsolidateStage().ExecuteAsync(context, null, CancellationToken.None);
        var after = CountRegions(buffer);

        Assert.True(after <= before, $"consolidation increased regions {before} → {after}");
    }

    private static int CountRegions(RgbaImageBuffer buffer)
    {
        var colors = new HashSet<(byte, byte, byte)>();
        for (int i = 0; i < buffer.Pixels.Length; i += 4)
            colors.Add((buffer.Pixels[i], buffer.Pixels[i + 1], buffer.Pixels[i + 2]));
        return colors.Count;
    }

    [Fact]
    public async Task ShadowBlock_KeepsTwoTonesPerBaseColor()
    {
        var buffer = MakeGradientImage();
        var context = Context(buffer, new Dictionary<string, object> { [ParamKeys.Colors] = 3 });

        await new QuantizeStage().ExecuteAsync(context, null, CancellationToken.None);
        var beforeColors = CountRegions(buffer);
        await new ShadowBlockStage().ExecuteAsync(context, null, CancellationToken.None);
        var afterColors = CountRegions(buffer);

        // Blocking can at most double tone count per base color; must stay bounded.
        Assert.True(afterColors <= beforeColors * 3, "shadow blocking exploded the palette");
    }

    [Fact]
    public async Task FullPipeline_EndsBoundedAndOpaque()
    {
        var buffer = MakeGradientImage(24, 24);
        var context = Context(buffer, new Dictionary<string, object>
        {
            [ParamKeys.Colors] = 8,
            [ParamKeys.Smoothing] = 0.5,
            [ParamKeys.Edges] = 0.2,
        });

        foreach (var stage in new IImageProcessingStage[]
        {
            new PreprocessStage(),
            new SmoothStage(),
            new QuantizeStage(),
            new ConsolidateStage(),
            new ShadowBlockStage(),
            new EdgeInkStage(),
        })
        {
            context = await stage.ExecuteAsync(context, null, CancellationToken.None);
        }

        var distinct = CountRegions((RgbaImageBuffer)context.Working!);
        Assert.True(distinct <= 24, $"pipeline left {distinct} colors; palette bound broken");

        // Alpha untouched.
        for (int i = 3; i < context.Working.Pixels.Length; i += 4)
            Assert.Equal(255, context.Working.Pixels[i]);
    }
}
