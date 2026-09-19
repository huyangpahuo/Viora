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

public sealed class TiffExporter : IImageExporter
{
    public string FormatId => "tiff";

    public string DisplayNameKey => "Export.Format.Tiff";

    public string FileExtension => ".tiff";

    public Task ExportAsync(IImageBuffer buffer, Stream output, IReadOnlyDictionary<string, object>? options, CancellationToken cancellationToken)
        => Exporters.ExportViaWicAsync(buffer, output, WicEncoder.Tiff, quality: null, cancellationToken);
}

public sealed class GifExporter : IImageExporter
{
    public string FormatId => "gif";

    public string DisplayNameKey => "Export.Format.Gif";

    public string FileExtension => ".gif";

    public Task ExportAsync(IImageBuffer buffer, Stream output, IReadOnlyDictionary<string, object>? options, CancellationToken cancellationToken)
        => Exporters.ExportViaWicAsync(buffer, output, WicEncoder.Gif, quality: null, cancellationToken);
}

/// <summary>
/// WebP 导出:依赖系统安装的 WebP 编解码扩展(WIC 组件,Windows 10/11 商店
/// "WebP Image Extensions" 或 VP8/V9 编码器)。未安装时抛出可识别异常,由调用方提示。
/// </summary>
public sealed class WebpExporter : IImageExporter
{
    /// <summary>WebP 容器格式 CLSID(官方 WebP codec for Windows 注册的 WIC 容器)。</summary>
    private static readonly Guid WebpContainerFormat = new("7693E886-51C9-4070-8419-9F70738EC8FA");

    public string FormatId => "webp";

    public string DisplayNameKey => "Export.Format.Webp";

    public string FileExtension => ".webp";

    public Task ExportAsync(IImageBuffer buffer, Stream output, IReadOnlyDictionary<string, object>? options, CancellationToken cancellationToken)
        => Exporters.ExportViaCustomContainerAsync(buffer, output, WebpContainerFormat, cancellationToken);

    public static bool IsSupported
    {
        get
        {
            try
            {
                _ = BitmapEncoder.Create(WebpContainerFormat);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

internal enum WicEncoder { Png, Jpeg, Bmp, Tiff, Gif }

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
                WicEncoder.Tiff => new TiffBitmapEncoder(),
                WicEncoder.Gif => new GifBitmapEncoder(),
                _ => new BmpBitmapEncoder(),
            };
            wic.Frames.Add(frame);
            wic.Save(output); // WIC is synchronous; we're on the thread pool already
        }, ct).ConfigureAwait(false);
    }

    /// <summary>按容器 CLSID 创建 WIC 编码器导出(用于系统可选编码器,如 WebP)。</summary>
    public static async Task ExportViaCustomContainerAsync(IImageBuffer buffer, Stream output, Guid containerFormat, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            var source = ToBitmapSource(buffer);
            var frame = BitmapFrame.Create(source);
            BitmapEncoder wic;
            try
            {
                wic = BitmapEncoder.Create(containerFormat);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("WebP encoder is not installed on this system.", ex);
            }
            wic.Frames.Add(frame);
            wic.Save(output);
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
