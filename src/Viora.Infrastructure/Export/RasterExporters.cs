using System.IO;
using System.Windows.Media.Imaging;
using Viora.Core.Imaging;
using Viora.Core.Plugins;

namespace Viora.Infrastructure.Export;

public sealed class PngExporter : IImageExporter
{
    public string FormatId => "png";

    public string DisplayNameKey => "Export.Format.Png";

    public string FileExtension => ".png";

    public Task ExportAsync(IImageBuffer buffer, Stream output, IReadOnlyDictionary<string, object>? options, CancellationToken cancellationToken)
        => Exporters.ExportViaWicAsync(buffer, output, WicEncoder.Png, quality: null, cancellationToken);
}

public sealed class JpegExporter : IImageExporter
{
    public string FormatId => "jpeg";

    public string DisplayNameKey => "Export.Format.Jpeg";

    public string FileExtension => ".jpg";

    public Task ExportAsync(IImageBuffer buffer, Stream output, IReadOnlyDictionary<string, object>? options, CancellationToken cancellationToken)
    {
        int quality = 92;
        if (options?.TryGetValue("quality", out var q) == true && q is int i && i is >= 1 and <= 100) quality = i;
        return Exporters.ExportViaWicAsync(buffer, output, WicEncoder.Jpeg, quality, cancellationToken);
    }
}

public sealed class BmpExporter : IImageExporter
{
    public string FormatId => "bmp";

    public string DisplayNameKey => "Export.Format.Bmp";

    public string FileExtension => ".bmp";

    public Task ExportAsync(IImageBuffer buffer, Stream output, IReadOnlyDictionary<string, object>? options, CancellationToken cancellationToken)
        => Exporters.ExportViaWicAsync(buffer, output, WicEncoder.Bmp, null, cancellationToken);
}

internal enum WicEncoder { Png, Jpeg, Bmp }

internal static class Exporters
{
    public static async Task ExportViaWicAsync(IImageBuffer buffer, Stream output, WicEncoder encoder, int? quality, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            var source = ToBitmapSource(buffer);
            var frame = BitmapFrame.Create(source);
            BitmapEncoder wic = encoder switch
            {
                WicEncoder.Png => new PngBitmapEncoder(),
                WicEncoder.Jpeg => new JpegBitmapEncoder { QualityLevel = quality ?? 92 },
                _ => new BmpBitmapEncoder(),
            };
            wic.Frames.Add(frame);
            wic.Save(output); // WIC is synchronous; we're on the thread pool already
        }, ct).ConfigureAwait(false);
    }

    private static System.Windows.Media.Imaging.BitmapSource ToBitmapSource(IImageBuffer buffer)
    {
        var bs = System.Windows.Media.Imaging.BitmapSource.Create(
            buffer.Width, buffer.Height, 96, 96,
            System.Windows.Media.PixelFormats.Bgra32, null,
            buffer.Pixels, buffer.Stride);
        bs.Freeze();
        return bs;
    }
}
