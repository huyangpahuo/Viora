using Viora.Core.Imaging;

namespace Viora.Infrastructure.Imaging;

/// <summary>Converts between framework image sources and the Core buffer format.</summary>
public interface IImportService
{
    /// <summary>Loads a file into a buffer, applying the max-dimension cap. Throws user-safe exceptions types on failure.</summary>
    Task<IImageBuffer> LoadFromFileAsync(string path, int maxDimension, CancellationToken cancellationToken = default);
}

public sealed class ImageImportException : Exception
{
    public ImageImportErrorCode ErrorCode { get; }

    public ImageImportException(ImageImportErrorCode code, string message, Exception? inner = null)
        : base(message, inner) => ErrorCode = code;
}

public enum ImageImportErrorCode
{
    NotFound,
    Unsupported,
    Corrupt,
    TooLarge,
    Unknown,
}
