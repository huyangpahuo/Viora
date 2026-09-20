using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Viora.UI.Pages.Settings;

/// <summary>设置页(通用/外观/性能/插件管理/关于)。数据与命令见 SettingsViewModel。</summary>
public partial class SettingsPage : UserControl
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }

    /// <summary>自定义主题色块点击:弹出系统取色器(含吸管与自定义色板)回写十六进制值。</summary>
    private void OnPickColor(object sender, System.Windows.RoutedEventArgs e)
    {
        if ((sender as System.Windows.FrameworkElement)?.DataContext
            is not ThemeColorRowViewModel row) return;

        Color initial;
        try { initial = (Color)ColorConverter.ConvertFromString(row.HexValue); }
        catch { initial = Colors.White; }

        var chosen = ChooseColorNative.Show(System.Windows.Application.Current.MainWindow, initial);
        if (chosen is null) return;

        row.HexValue = chosen.Value.A == 255
            ? $"#{chosen.Value.R:X2}{chosen.Value.G:X2}{chosen.Value.B:X2}"
            : $"#{chosen.Value.A:X2}{chosen.Value.R:X2}{chosen.Value.G:X2}{chosen.Value.B:X2}";
    }

    /// <summary>快捷键录制:页面级 PreviewKeyDown 转发给 VM(仅录制中的行消费)。</summary>
    private void OnPageKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (DataContext is SettingsViewModel vm && vm.HandleRecordKey(e))
            e.Handled = true;
    }
}

/// <summary>comdlg32 标准取色对话框(含展开的自定义色板),避免引入 WinForms 命名空间冲突。</summary>
internal static class ChooseColorNative
{
    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode)]
    private static extern bool ChooseColor(ref CHOOSECOLOR lpcc);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CHOOSECOLOR
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public int rgbResult;
        public IntPtr lpCustColors;
        public int Flags;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
    }

    private const int CC_RGBINIT = 0x1;
    private const int CC_FULLOPEN = 0x2;

    /// <summary>打开取色器;取消返回 null。</summary>
    public static Color? Show(Window? owner, Color initial)
    {
        var cust = Marshal.AllocHGlobal(16 * sizeof(int));
        try
        {
            for (var i = 0; i < 16; i++)
                Marshal.WriteInt32(cust, i * sizeof(int), 0x00FFFFFF);

            var cc = new CHOOSECOLOR
            {
                lStructSize = Marshal.SizeOf<CHOOSECOLOR>(),
                hwndOwner = owner is null ? IntPtr.Zero : new System.Windows.Interop.WindowInteropHelper(owner).Handle,
                rgbResult = initial.R | (initial.G << 8) | (initial.B << 16),
                lpCustColors = cust,
                Flags = CC_RGBINIT | CC_FULLOPEN,
            };
            if (!ChooseColor(ref cc)) return null;
            return Color.FromRgb((byte)(cc.rgbResult & 0xFF), (byte)((cc.rgbResult >> 8) & 0xFF), (byte)((cc.rgbResult >> 16) & 0xFF));
        }
        finally
        {
            Marshal.FreeHGlobal(cust);
        }
    }
}
