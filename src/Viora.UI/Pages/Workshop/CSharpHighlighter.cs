using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Documents;

namespace Viora.UI.Pages.Workshop;

/// <summary>
/// 轻量 C# 语法高亮:正则 tokenizer,把源码着色为 RichTextBox 流文档。
/// 颜色取自主题令牌,随配色方案联动;只处理当前可见语义层,不做完整解析。
/// </summary>
public static partial class CSharpHighlighter
{
    [GeneratedRegex(
        @"(?<comment>//.*$|/\*.*?\*/)" +
        @"|(?<string>""""?(?:[^""\\\n]|\\.)*""|'(?:[^'\\\n]|\\.)*')" +
        @"|(?<number>\b\d+(?:\.\d+)[fFmMdD]?\b|\b\d+[fFmMdDlL]\b|\b0[xX][0-9a-fA-F]+\b|\b\d+\b)" +
        @"|(?<keyword>\b(?:using|namespace|public|private|protected|internal|sealed|abstract|static|readonly|class|record|struct|interface|enum|new|return|if|else|for|foreach|while|do|switch|case|break|continue|try|catch|finally|throw|null|true|false|this|base|override|virtual|async|await|in|out|ref|params|is|as|not|and|or|default|typeof|nameof|get|set|var|partial|where|when|yield)\b)" +
        @"|(?<type>\b[A-Z][A-Za-z0-9_]*\b)" +
        @"|(?<usings>^\s*using\s+[\w.]+\s*;)",
        RegexOptions.Multiline | RegexOptions.ExplicitCapture)]
    private static partial Regex TokenRegex();

    /// <summary>把 C# 源码渲染到 FlowDocument(段落=行,带行号列由 UI 层负责)。</summary>
    public static FlowDocument Build(string source, IHighlightPalette palette, bool minimap = false)
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = new FontFamily("Consolas"),
            FontSize = minimap ? 2.2 : 12.5,
        };

        var lines = source.Replace("\r\n", "\n").Split('\n');
        foreach (var lineText in lines)
        {
            var paragraph = new Paragraph { Margin = new Thickness(0) };
            if (lineText.Length == 0)
            {
                paragraph.Inlines.Add(new Run(" "));
                if (minimap) paragraph.LineHeight = 2;
                doc.Blocks.Add(paragraph);
                continue;
            }
            if (minimap) paragraph.LineHeight = 2.6;

            int last = 0;
            foreach (Match m in TokenRegex().Matches(lineText))
            {
                if (m.Index > last)
                    paragraph.Inlines.Add(Plain(lineText[last..m.Index], palette));
                paragraph.Inlines.Add(Colorize(m, palette));
                last = m.Index + m.Length;
            }
            if (last < lineText.Length)
                paragraph.Inlines.Add(Plain(lineText[last..], palette));
            doc.Blocks.Add(paragraph);
        }
        return doc;
    }

    private static Run Plain(string text, IHighlightPalette p) => new(text) { Foreground = p.Plain };

    private static Run Colorize(Match m, IHighlightPalette p)
    {
        var text = m.Value;
        if (m.Groups["comment"].Success) return new Run(text) { Foreground = p.Comment, FontStyle = FontStyles.Italic };
        if (m.Groups["string"].Success) return new Run(text) { Foreground = p.String };
        if (m.Groups["number"].Success) return new Run(text) { Foreground = p.Number };
        if (m.Groups["keyword"].Success) return new Run(text) { Foreground = p.Keyword, FontWeight = FontWeights.Medium };
        if (m.Groups["usings"].Success)
        {
            // using 行:关键字紫、命名空间青
            var run = new Run(text) { Foreground = p.Keyword };
            return run;
        }
        return new Run(text) { Foreground = p.Type };
    }

    /// <summary>高亮调色板(实现方从主题令牌取色,语言切换/换主题时重建)。</summary>
    public interface IHighlightPalette
    {
        Brush Plain { get; }
        Brush Comment { get; }
        Brush Keyword { get; }
        Brush Type { get; }
        Brush String { get; }
        Brush Number { get; }
    }

    /// <summary>从 WPF 资源令牌取色的默认调色板。</summary>
    public sealed class TokenPalette : IHighlightPalette
    {
        public static readonly TokenPalette Instance = new();

        private static Brush Token(string key, string fallback) =>
            System.Windows.Application.Current?.TryFindResource(key) as Brush
            ?? (Brush)(new BrushConverter().ConvertFromString(fallback));

        public Brush Plain => Token("Color.Text.Primary", "#E8E4DC");
        public Brush Comment => Token("Workshop.Syntax.Comment", "#8A9179");
        public Brush Keyword => Token("Workshop.Syntax.Keyword", "#C58FFF");
        public Brush Type => Token("Workshop.Syntax.Type", "#7CC7E8");
        public Brush String => Token("Workshop.Syntax.String", "#E8A2A2");
        public Brush Number => Token("Workshop.Syntax.Number", "#D8B27C");
    }
}
