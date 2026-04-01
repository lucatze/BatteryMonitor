Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Drawing.Common

$outputPath = "$PSScriptRoot\AppIcon.ico"
$sizes = @(16, 32, 48, 256)

$pngBytes = @()

foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode    = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    # Dark circle background
    $bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 13, 15, 20))
    $g.FillEllipse($bgBrush, 0, 0, $size - 1, $size - 1)

    # Lightning bolt ⚡ in AccentBlue
    $fgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 74, 158, 255))
    $fontSize = [float]($size * 0.56)
    $font = New-Object System.Drawing.Font(
        "Segoe UI", $fontSize,
        [System.Drawing.FontStyle]::Bold,
        [System.Drawing.GraphicsUnit]::Pixel)
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment     = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $rect = New-Object System.Drawing.RectangleF(0, 0, $size, $size)
    $g.DrawString([char]0x26A1, $font, $fgBrush, $rect, $sf)

    $g.Dispose()
    $font.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngBytes += , $ms.ToArray()
    $ms.Dispose()
    $bmp.Dispose()
}

# Build ICO binary
$ico = New-Object System.IO.MemoryStream
$w   = New-Object System.IO.BinaryWriter($ico)

# ICONDIR header
$w.Write([uint16]0)               # Reserved
$w.Write([uint16]1)               # Type = 1 (ICO)
$w.Write([uint16]$sizes.Count)   # Image count

# Image data starts after header (6) + directory entries (16 each)
$dataOffset = 6 + 16 * $sizes.Count

# ICONDIRENTRY for each size
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz  = $sizes[$i]
    $dim = if ($sz -eq 256) { 0 } else { $sz }
    $off = $dataOffset
    for ($j = 0; $j -lt $i; $j++) { $off += $pngBytes[$j].Length }

    $w.Write([byte]$dim)             # Width  (0 = 256)
    $w.Write([byte]$dim)             # Height (0 = 256)
    $w.Write([byte]0)                # ColorCount
    $w.Write([byte]0)                # Reserved
    $w.Write([uint16]1)              # Planes
    $w.Write([uint16]32)             # BitCount
    $w.Write([uint32]$pngBytes[$i].Length)  # Data size
    $w.Write([uint32]$off)           # Data offset
}

# PNG image data
foreach ($bytes in $pngBytes) { $w.Write($bytes) }
$w.Flush()

[System.IO.File]::WriteAllBytes($outputPath, $ico.ToArray())
Write-Host "Icon written: $outputPath  ($($ico.Length) bytes)"
