# Generates assets/viora.ico (multi-size PNG-in-ICO) from the source logo PNG.
# Usage: powershell -NoProfile -File tools/gen_icon.ps1 [source.png]
param(
    [string]$Source = "D:\Viora\assets\logo.png",
    [string]$Output = "D:\Viora\assets\viora.ico"
)

Add-Type -AssemblyName System.Drawing

$img = [System.Drawing.Image]::FromFile($Source)
try {
    # Center-crop to square so the icon is not distorted.
    $side = [Math]::Min($img.Width, $img.Height)
    $crop = New-Object System.Drawing.Rectangle ([int](($img.Width - $side) / 2)), ([int](($img.Height - $side) / 2)), $side, $side

    $sizes = 16, 24, 32, 48, 64, 128, 256
    $pngs = @()
    foreach ($s in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap $s, $s
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $dest = New-Object System.Drawing.Rectangle 0, 0, $s, $s
        $g.DrawImage($img, $dest, $crop, [System.Drawing.GraphicsUnit]::Pixel)
        $g.Dispose()

        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngs += , $ms.ToArray()
        $bmp.Dispose()
    }
}
finally { $img.Dispose() }

$fs = [System.IO.File]::Create($Output)
$bw = New-Object System.IO.BinaryWriter $fs
try {
    # ICONDIR
    $bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
    # ICONDIRENTRY per image (256 is encoded as 0)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $b = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
        $bw.Write([Byte]$b); $bw.Write([Byte]$b)   # width, height
        $bw.Write([Byte]0); $bw.Write([Byte]0)     # palette, reserved
        $bw.Write([UInt16]1); $bw.Write([UInt16]32) # planes, bit count
        $bw.Write([UInt32]$pngs[$i].Length)
        $bw.Write([UInt32]$offset)
        $offset += $pngs[$i].Length
    }
    foreach ($p in $pngs) { $bw.Write($p) }
}
finally { $bw.Dispose(); $fs.Dispose() }

Write-Host "ICO written: $Output ($((Get-Item $Output).Length) bytes)"
