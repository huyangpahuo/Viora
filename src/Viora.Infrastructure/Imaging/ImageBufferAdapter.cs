using Viora.Core.Imaging;

namespace Viora.Infrastructure.Imaging;

/// <summary>
/// Adapter registered in DI for buffer-level utilities; keeps Core free of WPF while
/// giving consumers a single entry point for framework interop helpers.
/// </summary>
public sealed class ImageBufferAdapter
{
    /// <summary>Creates a UI-bindable frozen BitmapSource from a Core buffer.</summary>
    public System.Windows.Media.Imaging.BitmapSource ToBitmapSource(IImageBuffer buffer)
    {
        var source = System.Windows.Media.Imaging.BitmapSource.Create(
            buffer.Width, buffer.Height, 96, 96,
            System.Windows.Media.PixelFormats.Bgra32, null,
            buffer.Pixels, buffer.Stride);
        source.Freeze();
        return source;
    }

    /// <summary>Copies from a BitmapSource into a Core buffer (BGRA32).</summary>
    public IImageBuffer FromBitmapSource(System.Windows.Media.Imaging.BitmapSource source)
    {
        var converted = new System.Windows.Media.Imaging.FormatConvertedBitmap(
            source, System.Windows.Media.PixelFormats.Bgra32, null, 0);
        converted.Freeze();
        int stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        return new RgbaImageBuffer(converted.PixelWidth, converted.PixelHeight, pixels);
    }
}
