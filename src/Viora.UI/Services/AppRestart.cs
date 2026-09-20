using System.Diagnostics;

namespace Viora.UI.Services;

/// <summary>重启当前应用(先拉起新进程再关闭当前实例)。</summary>
public static class AppRestart
{
    public static void Restart()
    {
        var exe = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exe))
        {
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        }
        System.Windows.Application.Current.Shutdown();
    }
}
