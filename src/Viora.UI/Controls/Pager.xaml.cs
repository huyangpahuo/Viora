using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Viora.UI.Controls;

/// <summary>
/// 分页条:「‹ 1 2 3 … 9 ›」。页数多时中段折叠为省略号;当前页高亮,首尾页禁用箭头。
/// 页码按钮为即时构建(页数有限,无需虚拟化)。
/// </summary>
public partial class Pager : UserControl
{
    public static readonly DependencyProperty TotalItemsProperty = DependencyProperty.Register(
        nameof(TotalItems), typeof(int), typeof(Pager), new PropertyMetadata(0, (_, _) => RebuildStatic()));

    public static readonly DependencyProperty PageSizeProperty = DependencyProperty.Register(
        nameof(PageSize), typeof(int), typeof(Pager), new PropertyMetadata(12, (_, _) => RebuildStatic()));

    public static readonly DependencyProperty CurrentPageProperty = DependencyProperty.Register(
        nameof(CurrentPage), typeof(int), typeof(Pager),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (_, _) => RebuildStatic()));

    private static readonly List<Pager> _instances = new();

    public Pager()
    {
        InitializeComponent();
        _instances.Add(this);
        DataContextChanged += (_, _) => Dispatcher.BeginInvoke(RebuildStatic);
    }

    public int TotalItems
    {
        get => (int)GetValue(TotalItemsProperty);
        set => SetValue(TotalItemsProperty, value);
    }

    public int PageSize
    {
        get => (int)GetValue(PageSizeProperty);
        set => SetValue(PageSizeProperty, value);
    }

    public int CurrentPage
    {
        get => (int)GetValue(CurrentPageProperty);
        set => SetValue(CurrentPageProperty, value);
    }

    private int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)Math.Max(1, PageSize)));

    private static void RebuildStatic()
    {
        foreach (var pager in _instances) pager.Rebuild();
    }

    private void Rebuild()
    {
        int total = TotalPages;
        int current = Math.Clamp(CurrentPage, 1, total);

        PrevButton.IsEnabled = current > 1;
        NextButton.IsEnabled = current < total;

        PageNumbers.Children.Clear();
        foreach (var slot in BuildSlots(total, current))
        {
            if (slot is null)
            {
                PageNumbers.Children.Add(new TextBlock
                {
                    Text = "…",
                    Margin = new Thickness(2, 0, 2, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = (Brush)FindResource("Color.Text.Disabled"),
                });
                continue;
            }

            int page = slot.Value;
            var button = new ToggleButton
            {
                Content = page.ToString(),
                Width = 28,
                Height = 28,
                Margin = new Thickness(1, 0, 1, 0),
                FocusVisualStyle = null,
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            button.SetResourceReference(StyleProperty, "PagerPageButton");
            button.SetCurrentValue(ToggleButton.IsCheckedProperty, page == current);
            button.Click += (_, _) => SetPage(page);
            PageNumbers.Children.Add(button);
        }
    }

    /// <summary>页码槽位:null = 省略号。总页数 ≤ 7 全显;否则折叠中段为 当前页±1。</summary>
    private static IEnumerable<int?> BuildSlots(int total, int current)
    {
        if (total <= 7)
        {
            for (int i = 1; i <= total; i++) yield return i;
            yield break;
        }

        yield return 1;
        int start = Math.Max(2, current - 1);
        int end = Math.Min(total - 1, current + 1);
        if (start > 2) yield return null;
        for (int i = start; i <= end; i++) yield return i;
        if (end < total - 1) yield return null;
        yield return total;
    }

    private void SetPage(int page)
    {
        int clamped = Math.Clamp(page, 1, TotalPages);
        if (clamped == CurrentPage) return;
        CurrentPage = clamped;
    }

    private void OnPrevClick(object sender, RoutedEventArgs e) => SetPage(CurrentPage - 1);

    private void OnNextClick(object sender, RoutedEventArgs e) => SetPage(CurrentPage + 1);
}
