using System.Diagnostics;
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

    public override void WriteLine(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        var stack = new StackTrace(2, true);
        _logger.LogError("WPF binding: {Message} || at {Frames}",
            message.TrimEnd(),
            string.Join(" <- ", stack.GetFrames()
                .Take(6)
                .Select(f => f.GetMethod()?.DeclaringType?.FullName + "." + f.GetMethod()?.Name)));
    }
}
