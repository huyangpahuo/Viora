Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
    public struct RECT { public int L, T, R, B; }
}
"@
$p = Get-Process Viora -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $p) { Write-Host 'no window'; exit 1 }
[Win]::ShowWindow($p.MainWindowHandle, 9) | Out-Null
[Win]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 900
$r = New-Object Win+RECT
[Win]::GetWindowRect($p.MainWindowHandle, [ref]$r) | Out-Null
$w = $r.R - $r.L; $h = $r.B - $r.T
$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$dc = $g.GetHdc()
[Win]::PrintWindow($p.MainWindowHandle, $dc, 2) | Out-Null
$g.ReleaseHdc($dc)
$bmp.Save("C:\Users\39300\AppData\Local\Temp\viora_shot.png")
Write-Host "saved $w x $h"
