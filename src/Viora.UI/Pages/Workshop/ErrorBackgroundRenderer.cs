using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using Viora.UI.Pages.Workshop;

namespace Viora.UI.Pages.Workshop;

/// <summary>
/// 实时纠错的错误/警告标记背景渲染器:错误 = 红色底 + 红色下划线,警告 = 琥珀下划线。
/// </summary>
public sealed class ErrorBackgroundRenderer : IBackgroundRenderer
{
    private readonly List<(TextSegment Segment, bool IsError)> _items = new();
    private static readonly Brush ErrorFill = new SolidColorBrush(Color.FromArgb(0x28, 0xE8, 0x3E, 0x3E));
    private static readonly Pen ErrorPen = new(new SolidColorBrush(Color.FromArgb(0xFF, 0xE8, 0x3E, 0x3E)), 1);
    private static readonly Pen WarnPen = new(new SolidColorBrush(Color.FromArgb(0xB0, 0xD8, 0xB2, 0x7C)), 1);

    public KnownLayer Layer => KnownLayer.Background;

    public void SetSegments(IReadOnlyList<WorkshopViewModel.DiagnosticEntry> entries)
    {
        _items.Clear();
        foreach (var e in entries)
            _items.Add((new TextSegment { StartOffset = e.Offset, Length = e.Length }, e.IsError));
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_items.Count == 0) return;
        foreach (var (segment, isError) in _items)
        {
            var rects = BackgroundGeometryBuilder.GetRectsForSegment(textView, segment).ToList();
            if (rects.Count == 0) continue;

            var fill = isError ? ErrorFill : Brushes.Transparent;
            var pen = isError ? ErrorPen : WarnPen;
            foreach (var rect in rects)
            {
                if (rect.Width <= 0 && rect.Height <= 0) continue;
                if (isError)
                {
                    // 错误:淡红底
                    drawingContext.DrawRectangle(fill, null, rect);
                }
                // 底部波浪线(错误红/警告琥珀)
                double y = rect.Bottom - 1.5;
                double seg = Math.Max(3, rect.Width / 6);
                int waves = Math.Max(1, (int)(rect.Width / seg));
                var pen2 = isError ? ErrorPen : WarnPen;
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(new Point(rect.Left, y), false, false);
                    for (int i = 0; i < waves; i++)
                    {
                        double x0 = rect.Left + i * seg;
                        ctx.LineTo(new Point(x0 + seg / 2, y - 2), true, false);
                        ctx.LineTo(new Point(x0 + seg, y), true, false);
                    }
                }
                geo.Freeze();
                drawingContext.DrawGeometry(null, pen2, geo);
            }
        }
    }
}
