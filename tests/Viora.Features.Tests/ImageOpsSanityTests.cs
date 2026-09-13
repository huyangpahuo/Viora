using Viora.Core.Imaging;
using Viora.Features.Convert.Styles.Core;
using Xunit;

namespace Viora.Features.Tests;

public class ImageOpsSanityTests
{
    [Fact]
    public void BoxBlurColor_KeepsColors()
    {
        var buf = new RgbaImageBuffer(8, 8);
        for (int i = 0; i < buf.Pixels.Length; i += 4)
        {
            buf.Pixels[i] = 30; buf.Pixels[i + 1] = 90; buf.Pixels[i + 2] = 200; buf.Pixels[i + 3] = 255;
        }

        var blurred = ImageOps.BoxBlurColor(buf.Pixels, buf.Stride, 8, 8, 2);

        Assert.Equal(30, blurred[0]);        // B preserved
        Assert.Equal(90, blurred[1]);
        Assert.Equal(200, blurred[2]);
        Assert.Equal(255, blurred[3]);       // alpha preserved
        Assert.Equal(30, blurred[4]);        // uniform image: next pixel identical
    }

    [Fact]
    public void FillTriangle_Draws()
    {
        var buf = new RgbaImageBuffer(32, 32);
        var px = buf.Pixels;
        var a = (X: 2.0, Y: 2.0);
        var b = (X: 30.0, Y: 2.0);
        var c = (X: 16.0, Y: 30.0);
        ImageOps.FillTriangle(px, buf.Stride, 32, 32, a, b, c, 40, 120, 220, shadeEdges: false);

        // centroid must be filled
        int i = 12 * buf.Stride + 16 * 4;
        Assert.Equal(40, px[i]);
        Assert.Equal(120, px[i + 1]);
        Assert.Equal(220, px[i + 2]);
    }
}
