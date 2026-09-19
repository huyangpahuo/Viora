using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Viora.App.Hosting;

/// <summary>
/// 把 WPF 绑定错误(DataBindingSource)转写入应用文件日志。绑定错误在 Visual Studio
/// 错误列表里往往只剩"找不到源"且无堆栈,写入日志后可凭调用堆栈精确定位来源。
/// </summary>
public sealed class BindingErrorTraceListener : TraceListener
{
    private readonly ILogger<App> _logger;

    public BindingErrorTraceListener(ILogger<App> logger) => _logger = logger;

    public override void Write(string? message) => WriteLine(message);

    private static int _dumped;

    public override void WriteLine(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        var stack = new StackTrace(2, true);
        _logger.LogError("WPF binding: {Message} || at {Frames}",
            message.TrimEnd(),
            string.Join(" <- ", stack.GetFrames()
                .Take(6)
                .Select(f => f.GetMethod()?.DeclaringType?.FullName + "." + f.GetMethod()?.Name)));

        // 首次对齐绑定错误时,扫描全部窗口视觉树,输出样式可疑的 ListBoxItem 及其父链,
        // 用于定位持续出现的 HorizontalContentAlignment/VerticalContentAlignment 绑定来源。
        if (message.Contains("HorizontalContentAlignment") && Interlocked.Exchange(ref _dumped, 1) == 0)
        {
            _ = System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    foreach (System.Windows.Window window in System.Windows.Application.Current.Windows)
                        Scan(window, window.Title);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "binding dump failed");
                }
            });
        }
    }

    private void Scan(System.Windows.DependencyObject node, string path)
    {
        if (node is System.Windows.Controls.ListBoxItem item)
        {
            var styleName = item.Style is null ? "<null=theme default>" : item.Style.Resources == null && !item.Style.IsSealed ? "custom" : "sealed";
            _logger.LogError("SUSPECT ListBoxItem style={Style} target={Type} path={Path}",
                item.Style is null ? "NULL(theme)" : item.Style.TargetType.Name + "/" + styleName,
                item.GetType().Name, path);
        }

        var children = System.Windows.Media.VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < children; i++)
            Scan(System.Windows.Media.VisualTreeHelper.GetChild(node, i), path);
    }
}
