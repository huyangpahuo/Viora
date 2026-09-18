using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Viora.UI.Controls;

/// <summary>
/// Renders a vector icon from the geometry library (Assets/Icons.xaml, generated from
/// icon库 SVGs). Size comes from Width/Height; the glyph always stretches uniformly.
/// Fill mode uses Foreground; stroke mode (StrokeThickness &gt; 0) outlines the path —
/// used by window-chrome glyphs that must read as outlines (e.g. maximize square).
/// </summary>
public sealed class VioraIcon : Control
{
    public static readonly DependencyProperty GeometryProperty = DependencyProperty.Register(
        nameof(Geometry), typeof(Geometry), typeof(VioraIcon), new PropertyMetadata(null));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness), typeof(double), typeof(VioraIcon), new PropertyMetadata(0d));

    public Geometry? Geometry
    {
        get => (Geometry?)GetValue(GeometryProperty);
        set => SetValue(GeometryProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }
}
