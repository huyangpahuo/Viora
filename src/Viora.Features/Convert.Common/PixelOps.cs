using Viora.Core.Imaging;

namespace Viora.Features.Convert.Common;

/// <summary>Shared low-level pixel helpers for conversion pipeline stages (managed, branch-lean hot paths).</summary>
public static class PixelOps
{
    public const int B = 0, G = 1, R = 2, A = 3;

    /// <summary>BT.601 luma of a BGRA quad.</summary>
    public static byte Luma(byte[] px, int i) =>
        (byte)((px[i + R] * 299 + px[i + G] * 587 + px[i + B] * 114) / 1000);

    /// <summary>Linear index of (x, y).</summary>
    public static int Index(int x, int y, int stride) => y * stride + x * 4;

    /// <summary>Clamps to 0..255.</summary>
    public static byte Clamp(int value) => (byte)(value < 0 ? 0 : value > 255 ? 255 : value);

    /// <summary>Squared Euclidean RGB distance.</summary>
    public static int Distance2(byte[] px, int i, in (byte B, byte G, byte R) c)
    {
        int db = px[i + B] - c.B, dg = px[i + G] - c.G, dr = px[i + R] - c.R;
        return db * db + dg * dg + dr * dr;
    }

    /// <summary>Draws a single-pixel antialiased-free line into the buffer (boundary inking).</summary>
    public static void DrawLine(byte[] px, int stride, int width, int height,
        double x0, double y0, double x1, double y1, in (byte B, byte G, byte R, byte A) color)
    {
        // DDA over the dominant axis — adequate for ink lines at 1px.
        double dx = x1 - x0, dy = y1 - y0;
        int steps = (int)Math.Ceiling(Math.Max(Math.Abs(dx), Math.Abs(dy)));
        if (steps == 0) { BlendPixel(px, stride, width, height, (int)Math.Round(x0), (int)Math.Round(y0), color); return; }

        for (int s = 0; s <= steps; s++)
        {
            double t = (double)s / steps;
            int x = (int)Math.Round(x0 + dx * t);
            int y = (int)Math.Round(y0 + dy * t);
            BlendPixel(px, stride, width, height, x, y, color);
        }
    }

    public static void BlendPixel(byte[] px, int stride, int width, int height, int x, int y,
        in (byte B, byte G, byte R, byte A) color)
    {
        if (x < 0 || y < 0 || x >= width || y >= height) return;
        int i = y * stride + x * 4;
        int alpha = color.A;
        px[i + B] = (byte)((color.B * alpha + px[i + B] * (255 - alpha)) / 255);
        px[i + G] = (byte)((color.G * alpha + px[i + G] * (255 - alpha)) / 255);
        px[i + R] = (byte)((color.R * alpha + px[i + R] * (255 - alpha)) / 255);
    }
}
