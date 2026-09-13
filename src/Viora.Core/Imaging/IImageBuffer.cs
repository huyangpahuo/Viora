namespace Viora.Core.Imaging;

/// <summary>
/// A decoded image buffer in 32-bit BGRA format, row-major, tightly packed.
/// Deliberately framework-free: no WPF/Skia types cross the Core boundary.
/// </summary>
public interface IImageBuffer
{
    int Width { get; }

    int Height { get; }

    /// <summary>Row stride in bytes (Width * 4 for the default tightly-packed layout).</summary>
    int Stride { get; }

    /// <summary>Raw BGRA pixel data. Index = y * Stride + x * 4.</summary>
    byte[] Pixels { get; }

    IImageBuffer Clone();
}

public sealed class RgbaImageBuffer : IImageBuffer
{
    public RgbaImageBuffer(int width, int height)
        : this(width, height, new byte[width * height * 4])
    {
    }

    public RgbaImageBuffer(int width, int height, byte[] pixels)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (pixels.Length < width * height * 4) throw new ArgumentException("Pixel array too small.", nameof(pixels));
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public int Stride => Width * 4;

    public byte[] Pixels { get; }

    public IImageBuffer Clone() => new RgbaImageBuffer(Width, Height, (byte[])Pixels.Clone());
}
