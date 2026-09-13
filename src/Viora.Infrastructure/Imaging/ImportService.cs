using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Viora.Core.Imaging;
using Viora.Infrastructure.Imaging;

namespace Viora.Infrastructure.Imaging;

/// <summary>
/// File import via WPF codecs (runs inside Viora.Infrastructure, the only layer
/// allowed to touch PresentationCore), plus buffer → BitmapSource adaptation for the UI.
/// </summary>
public sealed class ImportService : IImportService
{
    private static readonly string[] SupportedExtensions =
        { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".webp" };

    public async Task<IImageBuffer> LoadFromFileAsync(string path, int maxDimension, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
            throw new ImageImportException(ImageImportErrorCode.NotFound, $"File not found: {path}");

        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (!SupportedExtensions.Contains(ext))
            throw new ImageImportException(ImageImportErrorCode.Unsupported, $"Unsupported extension: {ext}");

        try
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                BitmapSource source;
                try
                {
                    source = LoadOriented(path);
                }
                catch (Exception ex)
                {
                    throw new ImageImportException(ImageImportErrorCode.Corrupt, "Decoder failed.", ex);
                }

                if (source.PixelWidth * (long)source.PixelHeight > 64_000_000)
                    throw new ImageImportException(ImageImportErrorCode.TooLarge, "Over 64 MP.");

                // Downscale to the cap with high-quality scaling.
                double scale = Math.Min(1.0, maxDimension / (double)Math.Max(source.PixelWidth, source.PixelHeight));
                if (scale < 1.0)
                {
                    int w = Math.Max(1, (int)Math.Round(source.PixelWidth * scale));
                    int h = Math.Max(1, (int)Math.Round(source.PixelHeight * scale));
                    var scaled = new TransformedBitmap(source, new ScaleTransform(scale, scale));
                    scaled.Freeze();
                    source = scaled;
                }

                var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
                bgra.Freeze();

                int stride = bgra.PixelWidth * 4;
                var pixels = new byte[stride * bgra.PixelHeight];
                bgra.CopyPixels(pixels, stride, 0);
                return (IImageBuffer)new RgbaImageBuffer(bgra.PixelWidth, bgra.PixelHeight, pixels);
            }, cancellationToken);
        }
        catch (ImageImportException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            throw new ImageImportException(ImageImportErrorCode.Unknown, "Import failed.", ex);
        }
    }

    /// <summary>Decodes and bakes EXIF orientation so pixels come out upright.</summary>
    private static BitmapSource LoadOriented(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var decoder = BitmapDecoder.Create(
            stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames.FirstOrDefault()
            ?? throw new ImageImportException(ImageImportErrorCode.Corrupt, "No frames.");

        const int OrientationId = 0x0112;
        var metadata = frame.Metadata as BitmapMetadata;
        object? raw = null;
        if (metadata is not null)
        {
            if (metadata.ContainsQuery("System.Photo.Orientation"))
                raw = metadata.GetQuery("System.Photo.Orientation");
            raw ??= metadata.GetQuery("/app1/ifd/exif/{ushort=" + OrientationId + "}");
        }
        uint orientation = raw is uint o ? o : 1u;

        BitmapSource upright = orientation switch
        {
            2 => new TransformedBitmap(frame, new ScaleTransform(-1, 1)),
            3 => new TransformedBitmap(frame, new RotateTransform(180)),
            4 => new TransformedBitmap(frame, new ScaleTransform(1, -1)),
            5 => new TransformedBitmap(frame, new TransformGroup { Children = { new RotateTransform(90), new ScaleTransform(-1, 1) } }),
            6 => new TransformedBitmap(frame, new RotateTransform(90)),
            7 => new TransformedBitmap(frame, new TransformGroup { Children = { new RotateTransform(90), new ScaleTransform(1, -1) } }),
            8 => new TransformedBitmap(frame, new RotateTransform(270)),
            _ => frame,
        };

        upright.Freeze();
        return upright;
    }
}

public static class BitmapSourceExtensions
{
    /// <summary>Wraps a Core buffer as a frozen, UI-bindable image source.</summary>
    public static BitmapSource ToBitmapSource(this IImageBuffer buffer)
    {
        var format = System.Windows.Media.PixelFormats.Bgra32;
        var source = BitmapSource.Create(
            buffer.Width, buffer.Height, 96, 96, format, null,
            buffer.Pixels, buffer.Stride);
        source.Freeze();
        return source;
    }
}
