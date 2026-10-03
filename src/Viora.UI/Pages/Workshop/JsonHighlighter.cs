using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Documents;

namespace Viora.UI.Pages.Workshop;

/// <summary>轻量 JSON 语法高亮:键 / 字符串 / 数字 / 字面量着色,用于 registry 条目预览。</summary>
public static partial class JsonHighlighter
{
    [GeneratedRegex(
        @"(?<key>""(?:[^""\\]|\\.)*""\s*:)" +
        @"|(?<string>""(?:[^""\\]|\\.)*"")" +
        @"|(?<number>-?\b\d+(?:\.\d+)?\b)" +
        @"|(?<literal>\b(?:true|false|null)\b)",
        RegexOptions.ExplicitCapture)]
    private static partial Regex TokenRegex();

    public static FlowDocument Build(string json)
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
        };
        var lines = json.Replace("\r\n", "\n").Split('\n');
        foreach (var lineText in lines)
        {
            var paragraph = new Paragraph { Margin = new Thickness(0) };
            if (lineText.Length == 0)
            {
                paragraph.Inlines.Add(new Run(" "));
                doc.Blocks.Add(paragraph);
                continue;
            }

            int last = 0;
            foreach (Match m in TokenRegex().Matches(lineText))
            {
                if (m.Index > last)
                    paragraph.Inlines.Add(new Run(lineText[last..m.Index]) { Foreground = Plain() });
                paragraph.Inlines.Add(Colorize(m));
                last = m.Index + m.Length;
            }
            if (last < lineText.Length)
                paragraph.Inlines.Add(new Run(lineText[last..]) { Foreground = Plain() });
            doc.Blocks.Add(paragraph);
        }
        return doc;
    }

    private static Brush Plain() =>
        System.Windows.Application.Current?.TryFindResource("Color.Text.Primary") as Brush ?? Brushes.Gray;

    private static Inline Colorize(Match m)
    {
        var text = m.Value;
        if (m.Groups["key"].Success)
        {
            // 键:键名着键色,冒号回正文色
            int colon = text.LastIndexOf(':');
            var span = new Span();
            span.Inlines.Add(new Run(text[..colon]) { Foreground = Key() });
            span.Inlines.Add(new Run(text[colon..]) { Foreground = Plain() });
            return span;
        }
        if (m.Groups["string"].Success) return new Run(text) { Foreground = Str() };
        if (m.Groups["number"].Success) return new Run(text) { Foreground = Num() };
        return new Run(text) { Foreground = Kw() };
    }

    private static Brush Key() => Token("Workshop.Syntax.Type", "#7CC7E8");
    private static Brush Str() => Token("Workshop.Syntax.String", "#E8A2A2");
    private static Brush Num() => Token("Workshop.Syntax.Number", "#D8B27C");
    private static Brush Kw() => Token("Workshop.Syntax.Keyword", "#C58FFF");

    private static Brush Token(string key, string fallback) =>
        System.Windows.Application.Current?.TryFindResource(key) as Brush
        ?? (Brush)(new BrushConverter().ConvertFromString(fallback));
}
