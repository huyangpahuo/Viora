using System.IO;
using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.AnimeVector.Stages;
using Viora.Features.Convert.Common;
using Viora.Infrastructure.Export;
using Xunit;

namespace Viora.Features.Tests;

/// <summary>
/// End-to-end: synthetic image → full Anime Vector pipeline (via the real engine) →
/// PNG export bytes. Proves the conversion workflow's processing + export chain headlessly.
/// </summary>
public class ConversionWorkflowE2ETests
{
    [Fact]
    public async Task PipelineToPngExport_ProducesValidImage()
    {
        // Synthetic "photo": smooth gradients + a few color masses (deterministic).
        var buffer = new RgbaImageBuffer(160, 120);
        for (int y = 0; y < 120; y++)
        {
            for (int x = 0; x < 160; x++)
            {
                int i = y * buffer.Stride + x * 4;
                bool mass = (x / 40 + y / 30) % 2 == 0;
                buffer.Pixels[i] = (byte)(mass ? 40 + x : 180 - y / 2);
                buffer.Pixels[i + 1] = (byte)(60 + y / 2);
                buffer.Pixels[i + 2] = (byte)(mass ? 200 - x / 2 : 90 + x / 3);
                buffer.Pixels[i + 3] = 255;
            }
        }

        var parameters = new Dictionary<string, object>
        {
            [ParamKeys.Colors] = 8,
            [ParamKeys.Smoothing] = 0.6,
            [ParamKeys.Detail] = 0.5,
            [ParamKeys.Shadows] = 0.6,
            [ParamKeys.Edges] = 0.25,
            [ParamKeys.Saturation] = 1.15,
        };

        var pipelineStages = new IImageProcessingStage[]
        {
            new PreprocessStage(),
            new SmoothStage(),
            new QuantizeStage(),
            new ConsolidateStage(),
            new ShadowBlockStage(),
            new EdgeInkStage(),
        };

        var engine = new Viora.Infrastructure.Pipeline.ImageConversionEngine();
        var result = await engine.ExecuteAsync(
            pipelineStages, buffer, parameters, previewQuality: false,
            progress: null, cancellationToken: CancellationToken.None);

        Assert.Equal(160, result.Result.Width);
        Assert.Equal(120, result.Result.Height);

        // Export the result through the real WIC PNG exporter.
        string path = Path.Combine(Path.GetTempPath(), $"viora-e2e-{Guid.NewGuid():N}.png");
        try
        {
            using (var stream = File.Create(path))
            {
                await new PngExporter().ExportAsync(result.Result, stream, null, CancellationToken.None);
                await stream.FlushAsync();
            }
            var bytes = await File.ReadAllBytesAsync(path);
            Assert.True(bytes.Length > 8);
            Assert.Equal((byte)0x89, bytes[0]); // PNG magic
            Assert.Equal((byte)0x50, bytes[1]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Engine_ReportsProgressAndCompletes()
    {
        var buffer = new RgbaImageBuffer(64, 64);
        var pipelineStages = new IImageProcessingStage[]
        {
            new QuantizeStage(),
            new ConsolidateStage(),
        };
        var parameters = new Dictionary<string, object> { [ParamKeys.Colors] = 4 };

        var reports = new List<double>();
        var progress = new Progress<PipelineProgress>(p => reports.Add(p.OverallFraction));
        var engine = new Viora.Infrastructure.Pipeline.ImageConversionEngine();

        await engine.ExecuteAsync(pipelineStages, buffer, parameters, true, progress, CancellationToken.None);

        // Give the Progress<T> queue a moment to deliver to the sync context.
        await Task.Delay(200);
        Assert.Contains(1.0, reports.Select(r => Math.Round(r, 2)));
    }
}
