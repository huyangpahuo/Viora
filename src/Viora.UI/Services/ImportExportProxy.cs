using System.IO;
using System.Windows;
using System.Windows.Media;
using Viora.Core.Imaging;

namespace Viora.UI.Services;

/// <summary>
/// UI-side import/export facade. Viora.UI cannot reference Viora.Infrastructure
/// (layering rule), so the concrete behavior is assigned by the composition root (Viora.App).
/// </summary>
public enum ImageImportProxyErrorCode { NotFound, Unsupported, Corrupt, TooLarge, Unknown }

public class ImageImportProxyException : Exception
{
    public ImageImportProxyErrorCode ErrorCode { get; }

    public ImageImportProxyException(ImageImportProxyErrorCode code, string message, Exception? inner = null)
        : base(message, inner) => ErrorCode = code;
}

public interface IImportServiceProxy
{
    Task<IImageBuffer> LoadFromFileAsync(string path, int maxDimension, CancellationToken ct = default);

    ImageSource ToImageSource(IImageBuffer buffer);

    string? PickFile();
}

public interface IExportProxy
{
    string? PickSavePath(string defaultFormatId);

    Task ExportAsync(IImageBuffer buffer, string path, int jpegQuality, CancellationToken ct = default);
}
