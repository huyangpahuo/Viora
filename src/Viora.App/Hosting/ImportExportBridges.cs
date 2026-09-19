using System.IO;
using System.Windows;
using System.Windows.Media;
using Viora.Core.Imaging;
using Viora.Core.Plugins;
using Viora.Infrastructure.Export;
using Viora.Infrastructure.Imaging;
using Viora.UI.Services;

namespace Viora.App.Hosting;

/// <summary>
/// Concrete import/export bridges living in the composition root, where UI-facing
/// proxies are legitimately wired to Infrastructure implementations.
/// </summary>
public sealed class AppImportService : IImportServiceProxy
{
    private readonly IImportService _inner;

    public AppImportService(IImportService inner) => _inner = inner;

    public async Task<IImageBuffer> LoadFromFileAsync(string path, int maxDimension, CancellationToken ct = default)
    {
        try
        {
            return await _inner.LoadFromFileAsync(path, maxDimension, ct);
        }
        catch (ImageImportException ex)
        {
            throw new ImageImportProxyException((ImageImportProxyErrorCode)ex.ErrorCode, ex.Message, ex);
        }
    }

    public ImageSource ToImageSource(IImageBuffer buffer) => buffer.ToBitmapSource();

    public string? PickFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.webp|All files|*.*",
        };
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FileName : null;
    }

    public string[]? PickFiles()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.webp|All files|*.*",
            Multiselect = true,
        };
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FileNames : null;
    }
}

public sealed class AppExportService : IExportProxy
{
    public string? PickSavePath(string defaultFormatId)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            // 列出全部支持格式供随时切换;默认选中设置的格式
            Filter = "PNG|*.png|JPEG|*.jpg;*.jpeg|Bitmap|*.bmp|TIFF|*.tif;*.tiff|GIF|*.gif|WebP|*.webp",
            DefaultExt = defaultFormatId switch
            {
                "jpeg" => ".jpg",
                "bmp" => ".bmp",
                "tiff" => ".tiff",
                "gif" => ".gif",
                "webp" => ".webp",
                _ => ".png",
            },
            FilterIndex = defaultFormatId switch
            {
                "jpeg" => 2,
                "bmp" => 3,
                "tiff" => 4,
                "gif" => 5,
                "webp" => 6,
                _ => 1,
            },
        };
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FileName : null;
    }

    public async Task ExportAsync(IImageBuffer buffer, string path, int jpegQuality, CancellationToken ct = default)
    {
        IImageExporter exporter = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => new JpegExporter(),
            ".bmp" => new BmpExporter(),
            ".tif" or ".tiff" => new TiffExporter(),
            ".gif" => new GifExporter(),
            ".webp" => new WebpExporter(),
            _ => new PngExporter(),
        };
        await using var stream = File.Create(path);
        await exporter.ExportAsync(buffer, stream, new Dictionary<string, object> { ["quality"] = jpegQuality }, ct);
    }
}
