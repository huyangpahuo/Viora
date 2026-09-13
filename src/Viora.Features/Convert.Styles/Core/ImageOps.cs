using Viora.Core.Imaging;

namespace Viora.Features.Convert.Styles.Core;

/// <summary>
/// Shared raster primitives for the built-in style presets: separable blurs, Sobel,
/// posterize/dither LUTs, palette mapping, deterministic noise, bilinear sampling,
/// triangle fill and small bitmap glyphs. All pure managed BGRA, parallel-friendly.
/// </summary>
public static class ImageOps
{
    // ---------- Per-pixel access ----------

    public static int Index(int x, int y, int stride) => y * stride + x * 4;

    /// <summary>Sets every pixel's alpha to 255 — new pixel arrays start fully transparent.</summary>
    public static void OpaqueAlpha(byte[] px)
    {
        for (int i = 3; i < px.Length; i += 4) px[i] = 255;
    }

    public static int Luma(byte[] px, int i) => (px[i + 2] * 299 + px[i + 1] * 587 + px[i] * 114) / 1000;

    public static byte Clamp(int v) => (byte)(v < 0 ? 0 : v > 255 ? 255 : v);

    public static double ClampD(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;

    // ---------- Luminance / edges ----------

    /// <summary>Single-channel luma map (Width × Height).</summary>
    public static byte[] LumaMap(IImageBuffer buf)
    {
        var map = new byte[buf.Width * buf.Height];
        int w = buf.Width, stride = buf.Stride;
        for (int y = 0; y < buf.Height; y++)
            for (int x = 0; x < w; x++)
                map[y * w + x] = (byte)Luma(buf.Pixels, y * stride + x * 4);
        return map;
    }

    /// <summary>Sobel gradient magnitude of a luma map (0..~1020 floats).</summary>
    public static float[] SobelMagnitude(byte[] luma, int width, int height)
    {
        var mag = new float[width * height];
        for (int y = 0; y < height; y++)
        {
            int ym = Math.Max(0, y - 1), yp = Math.Min(height - 1, y + 1);
            for (int x = 0; x < width; x++)
            {
                int xm = Math.Max(0, x - 1), xp = Math.Min(width - 1, x + 1);
                int gx =
                    -luma[ym * width + xm] - 2 * luma[y * width + xm] - luma[yp * width + xm]
                    + luma[ym * width + xp] + 2 * luma[y * width + xp] + luma[yp * width + xp];
                int gy =
                    -luma[ym * width + xm] - 2 * luma[ym * width + x] - luma[ym * width + xp]
                    + luma[yp * width + xm] + 2 * luma[yp * width + x] + luma[yp * width + xp];
                mag[y * width + x] = (float)Math.Sqrt(gx * gx + gy * gy);
            }
        }
        return mag;
    }

    // ---------- Blur ----------

    /// <summary>In-place separable box blur on a gray map.</summary>
    public static void BoxBlurGray(byte[] map, int width, int height, int radius)
    {
        if (radius <= 0) return;
        var tmp = new byte[map.Length];
        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                int sum = 0, n = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int q = x + k;
                    if (q < 0 || q >= width) continue;
                    sum += map[row + q]; n++;
                }
                tmp[row + x] = (byte)(sum / n);
            }
        }
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int sum = 0, n = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int q = y + k;
                    if (q < 0 || q >= height) continue;
                    sum += tmp[q * width + x]; n++;
                }
                map[y * width + x] = (byte)(sum / n);
            }
        }
    }

    /// <summary>Box blur of an RGB float map (3 channels interleaved, row-major W×H×3).</summary>
    public static void BoxBlurRgb(float[] rgb, int width, int height, int radius)
    {
        var tmp = new float[rgb.Length];
        for (int y = 0; y < height; y++)
        {
            int row = y * width * 3;
            for (int x = 0; x < width; x++)
            {
                for (int c = 0; c < 3; c++)
                {
                    float sum = 0; int n = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int q = x + k;
                        if (q < 0 || q >= width) continue;
                        sum += rgb[row + q * 3 + c]; n++;
                    }
                    tmp[row + x * 3 + c] = sum / n;
                }
            }
        }
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int c = 0; c < 3; c++)
                {
                    float sum = 0; int n = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int q = y + k;
                        if (q < 0 || q >= height) continue;
                        sum += tmp[q * width * 3 + x * 3 + c]; n++;
                    }
                    rgb[y * width * 3 + x * 3 + c] = sum / n;
                }
            }
        }
    }

    /// <summary>
    /// In-place separable box blur on a BGRA byte buffer (alpha preserved).
    /// Returns a NEW blurred pixel array; the source is untouched.
    /// </summary>
    public static byte[] BoxBlurColor(byte[] px, int stride, int width, int height, int radius)
    {
        if (radius <= 0) return (byte[])px.Clone();
        var tmp = new byte[px.Length];
        var tmpStride = width * 4;

        for (int y = 0; y < height; y++)
        {
            int row = y * stride;
            int trow = y * tmpStride;
            for (int x = 0; x < width; x++)
            {
                int sb = 0, sg = 0, sr = 0, n = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int q = x + k;
                    if (q < 0 || q >= width) continue;
                    int i = row + q * 4;
                    sb += px[i]; sg += px[i + 1]; sr += px[i + 2]; n++;
                }
                int t = trow + x * 4;
                tmp[t] = (byte)(sb / n); tmp[t + 1] = (byte)(sg / n); tmp[t + 2] = (byte)(sr / n);
            }
        }

        var result = new byte[px.Length];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int sb = 0, sg = 0, sr = 0, n = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int q = y + k;
                    if (q < 0 || q >= height) continue;
                    int t = q * tmpStride + x * 4;
                    sb += tmp[t]; sg += tmp[t + 1]; sr += tmp[t + 2]; n++;
                }
                int o = y * stride + x * 4;
                result[o] = (byte)(sb / n); result[o + 1] = (byte)(sg / n); result[o + 2] = (byte)(sr / n);
                result[o + 3] = px[o + 3];
            }
        }
        return result;
    }

    // ---------- Tone ----------

    /// <summary>Contrast S-curve LUT: amount 0..1.</summary>
    public static byte[] ContrastLut(double amount)
    {
        var lut = new byte[256];
        double a = ClampD(amount, 0, 1);
        for (int v = 0; v < 256; v++)
        {
            double t = v / 255.0;
            double s = t < 0.5
                ? 0.5 * Math.Pow(2 * t, 1 + 3 * a)
                : 1 - 0.5 * Math.Pow(2 * (1 - t), 1 + 3 * a);
            lut[v] = Clamp((int)Math.Round(s * 255));
        }
        return lut;
    }

    /// <summary>Per-channel posterize LUT for n levels.</summary>
    public static byte[] PosterizeLut(int levels)
    {
        levels = Math.Clamp(levels, 2, 64);
        double step = 255.0 / (levels - 1);
        var lut = new byte[256];
        for (int v = 0; v < 256; v++) lut[v] = (byte)(Math.Round(v / step) * step);
        return lut;
    }

    public static byte GammaLut(byte v, double gamma) =>
        Clamp((int)Math.Round(Math.Pow(v / 255.0, gamma) * 255));

    // ---------- Palette ----------

    /// <summary>Picks up to maxColors representative colors from an RGB444 histogram.</summary>
    public static List<(int B, int G, int R)> ExtractPalette(IImageBuffer buf, int maxColors, int minDistance = 48)
    {
        var counts = new int[4096];
        var sumB = new long[4096]; var sumG = new long[4096]; var sumR = new long[4096];
        var px = buf.Pixels;
        int stride = buf.Stride;
        for (int y = 0; y < buf.Height; y++)
        {
            for (int x = 0; x < buf.Width; x++)
            {
                int i = y * stride + x * 4;
                int key = (px[i] >> 4) << 8 | (px[i + 1] >> 4) << 4 | (px[i + 2] >> 4);
                counts[key]++;
                sumB[key] += px[i]; sumG[key] += px[i + 1]; sumR[key] += px[i + 2];
            }
        }

        var palette = new List<(int B, int G, int R)>();
        foreach (var k in Enumerable.Range(0, 4096).Where(k => counts[k] > 0).OrderByDescending(k => counts[k]))
        {
            if (palette.Count >= maxColors) break;
            var c = (B: (int)(sumB[k] / counts[k]), G: (int)(sumG[k] / counts[k]), R: (int)(sumR[k] / counts[k]));
            if (palette.Any(p =>
                {
                    int db = p.B - c.B, dg = p.G - c.G, dr = p.R - c.R;
                    return db * db + dg * dg + dr * dr < minDistance * minDistance;
                })) continue;
            palette.Add(c);
        }
        if (palette.Count == 0) palette.Add((128, 128, 128));
        return palette;
    }

    public static int NearestPaletteIndex(List<(int B, int G, int R)> palette, int b, int g, int r)
    {
        int best = 0, bestDist = int.MaxValue;
        for (int p = 0; p < palette.Count; p++)
        {
            int db = palette[p].B - b, dg = palette[p].G - g, dr = palette[p].R - r;
            int d = db * db + dg * dg + dr * dr;
            if (d < bestDist) { bestDist = d; best = p; }
        }
        return best;
    }

    // ---------- Noise ----------

    /// <summary>Deterministic 0..1 hash noise from integer coordinates (reseeded per use).</summary>
    public static double Hash(int x, int y, int seed = 0)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 974634551;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535.0;
        }
    }

    /// <summary>4×4 Bayer matrix value (0..15) / 16.</summary>
    public static double Bayer4(int x, int y)
    {
        int[,] b =
        {
            { 0, 8, 2, 10 },
            { 12, 4, 14, 6 },
            { 3, 11, 1, 9 },
            { 15, 7, 13, 5 },
        };
        return b[y & 3, x & 3] / 16.0;
    }

    // ---------- Sampling ----------

    /// <summary>Bilinear BGRA sample at fractional coordinates (edge-clamped).</summary>
    public static void SampleBilinear(byte[] px, int stride, int width, int height, double fx, double fy, byte[] outQuad)
    {
        fx = ClampD(fx, 0, width - 1.001);
        fy = ClampD(fy, 0, height - 1.001);
        int x0 = (int)fx, y0 = (int)fy;
        int x1 = Math.Min(width - 1, x0 + 1), y1 = Math.Min(height - 1, y0 + 1);
        double ax = fx - x0, ay = fy - y0;

        for (int c = 0; c < 4; c++)
        {
            double v00 = px[y0 * stride + x0 * 4 + c];
            double v10 = px[y0 * stride + x1 * 4 + c];
            double v01 = px[y1 * stride + x0 * 4 + c];
            double v11 = px[y1 * stride + x1 * 4 + c];
            outQuad[c] = Clamp((int)Math.Round(
                v00 * (1 - ax) * (1 - ay) + v10 * ax * (1 - ay) + v01 * (1 - ax) * ay + v11 * ax * ay));
        }
    }

    /// <summary>Draws a filled axis-aligned circle (used by halftone, chalk, stitch dots).</summary>
    public static void FillCircle(byte[] px, int stride, int width, int height,
        double cx, double cy, double radius, int b, int g, int r)
    {
        int x0 = Math.Max(0, (int)(cx - radius)), x1 = Math.Min(width - 1, (int)Math.Ceiling(cx + radius));
        int y0 = Math.Max(0, (int)(cy - radius)), y1 = Math.Min(height - 1, (int)Math.Ceiling(cy + radius));
        double r2 = radius * radius;
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                double dx = x + 0.5 - cx, dy = y + 0.5 - cy;
                if (dx * dx + dy * dy > r2) continue;
                int i = y * stride + x * 4;
                px[i] = (byte)b; px[i + 1] = (byte)g; px[i + 2] = (byte)r;
            }
        }
    }

    /// <summary>Fills a triangle with a flat color using barycentric coverage (no AA).</summary>
    public static void FillTriangle(byte[] px, int stride, int width, int height,
        (double X, double Y) a, (double X, double Y) b, (double X, double Y) c,
        int colorB, int colorG, int colorR, bool shadeEdges = false)
    {
        double minX = Math.Max(0, Math.Min(a.X, Math.Min(b.X, c.X)));
        double maxX = Math.Min(width - 1, Math.Max(a.X, Math.Max(b.X, c.X)));
        double minY = Math.Max(0, Math.Min(a.Y, Math.Min(b.Y, c.Y)));
        double maxY = Math.Min(height - 1, Math.Max(a.Y, Math.Max(b.Y, c.Y)));
        if (minX > maxX || minY > maxY) return;

        double den = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
        if (Math.Abs(den) < 1e-9) return;

        for (int y = (int)minY; y <= (int)maxY; y++)
        {
            for (int x = (int)minX; x <= (int)maxX; x++)
            {
                double pxx = x + 0.5, pyy = y + 0.5;
                double w0 = ((b.Y - c.Y) * (pxx - c.X) + (c.X - b.X) * (pyy - c.Y)) / den;
                double w1 = ((c.Y - a.Y) * (pxx - c.X) + (a.X - c.X) * (pyy - c.Y)) / den;
                double w2 = 1 - w0 - w1;
                if (w0 < 0 || w1 < 0 || w2 < 0) continue;

                // tiny edge darkening keeps facets readable
                double edge = shadeEdges ? 1.0 - 0.12 * (1 - Math.Min(w0, Math.Min(w1, w2)) * 4) : 1.0;
                int i = y * stride + x * 4;
                px[i] = Clamp((int)(colorB * edge));
                px[i + 1] = Clamp((int)(colorG * edge));
                px[i + 2] = Clamp((int)(colorR * edge));
            }
        }
    }

    // ---------- 5×7 glyphs for ASCII art (" .:-=+*#%@") ----------

    public static readonly string[] AsciiChars = { " ", ".", ":", "-", "=", "+", "*", "#", "%", "@" };

    /// <summary>5×7 column bitmaps (LSB = top row); column order left→right, 5 columns per glyph.</summary>
    public static readonly byte[][] AsciiGlyphs =
    {
        new byte[5],                                     // space
        new byte[] { 0x00, 0x00, 0x00, 0x00, 0x01 },     // .
        new byte[] { 0x01, 0x01, 0x01, 0x01, 0x01 },     // :
        new byte[] { 0x00, 0x00, 0x1F, 0x00, 0x00 },     // -
        new byte[] { 0x0E, 0x0E, 0x1F, 0x0E, 0x0E },     // =  (dense mid band)
        new byte[] { 0x04, 0x04, 0x1F, 0x04, 0x04 },     // +
        new byte[] { 0x0A, 0x0A, 0x1F, 0x04, 0x0A },     // *
        new byte[] { 0x1F, 0x11, 0x11, 0x11, 0x1F },     // #
        new byte[] { 0x1F, 0x1F, 0x0E, 0x1F, 0x1F },     // %
        new byte[] { 0x1F, 0x1F, 0x1F, 0x1F, 0x1F },     // @
    };

    /// <summary>Whether the ASCII glyph bit at (col, row) is set.</summary>
    public static bool AsciiBit(int glyphIndex, int col, int row) =>
        (AsciiGlyphs[glyphIndex][col] & (1 << row)) != 0;
}
