using Viora.Core.Pipeline;
using Viora.Core.Plugins;
using Viora.Core.Imaging;
using Viora.Infrastructure.Pipeline;
using Xunit;

namespace Viora.Core.Tests;

public class VersionRangeTests
{
    [Theory]
    [InlineData("*", "1.0.0", true)]
    [InlineData("*", "99.0.0", true)]
    [InlineData(">=1.0.0", "1.0.0", true)]
    [InlineData(">=1.0.0", "0.9.9", false)]
    [InlineData(">=1.0.0 <2.0.0", "1.5.0", true)]
    [InlineData(">=1.0.0 <2.0.0", "2.0.0", false)]
    [InlineData(">=1.0.0 <2.0.0", "1.9.9", true)]
    public void Contains_EvaluatesBounds(string raw, string version, bool expected)
    {
        var range = VersionRange.Parse(raw);
        Assert.Equal(expected, range.Contains(Version.Parse(version)));
    }

    [Fact]
    public void Parse_HandlesTwoPartVersion()
    {
        var range = VersionRange.Parse(">=1.2");
        Assert.True(range.Contains(new Version(1, 2, 5)));
        Assert.False(range.Contains(new Version(1, 1, 0)));
    }
}

public class ImageBufferTests
{
    [Fact]
    public void Buffer_HasTightlyPackedStride()
    {
        var buffer = new RgbaImageBuffer(4, 3);
        Assert.Equal(16, buffer.Stride);
        Assert.Equal(48, buffer.Pixels.Length);
    }

    [Fact]
    public void Clone_ProducesIndependentCopy()
    {
        var buffer = new RgbaImageBuffer(2, 2);
        buffer.Pixels[0] = 42;
        var clone = buffer.Clone();
        clone.Pixels[0] = 7;
        Assert.Equal(42, buffer.Pixels[0]);
    }

    [Fact]
    public void Buffer_RejectsTooSmallPixelArray()
    {
        Assert.Throws<ArgumentException>(() => new RgbaImageBuffer(4, 4, new byte[10]));
    }
}

public class ConversionEngineTests
{
    private sealed class CountingStage : IImageProcessingStage
    {
        public string Name { get; }
        public List<string> Log { get; }

        public CountingStage(string name, List<string> log) { Name = name; Log = log; }

        public Task<ImageProcessingContext> ExecuteAsync(
            ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken cancellationToken)
        {
            Log.Add(Name);
            progress?.Report(new StageProgress(Name, 0, 1, 1.0));
            return Task.FromResult(context);
        }
    }

    [Fact]
    public async Task Engine_ExecutesStagesInOrder()
    {
        var log = new List<string>();
        var pipeline = new IImageProcessingStage[]
        {
            new CountingStage("A", log),
            new CountingStage("B", log),
            new CountingStage("C", log),
        };
        var engine = new ImageConversionEngine();
        var result = await engine.ExecuteAsync(
            pipeline, new RgbaImageBuffer(2, 2), new Dictionary<string, object>(), false, null, CancellationToken.None);

        Assert.Equal(new[] { "A", "B", "C" }, log);
        Assert.NotNull(result.Result);
    }

    [Fact]
    public async Task Engine_PropagatesCancellation()
    {
        var cts = new CancellationTokenSource();
        var throwing = new CancelDetectingStage(cts);
        var engine = new ImageConversionEngine();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            engine.ExecuteAsync(
                new[] { throwing }, new RgbaImageBuffer(2, 2), new Dictionary<string, object>(),
                false, null, cts.Token));
    }

    private sealed class CancelDetectingStage : IImageProcessingStage
    {
        private readonly CancellationTokenSource _cts;
        public CancelDetectingStage(CancellationTokenSource cts) => _cts = cts;
        public string Name => "Cancel";

        public Task<ImageProcessingContext> ExecuteAsync(
            ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken cancellationToken)
        {
            _cts.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(context);
        }
    }
}
