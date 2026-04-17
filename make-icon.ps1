param(
    [string]$Source = "src\MAXsCursor\Assets\cursor.png",
    [string]$Output = "src\MAXsCursor\Assets\cursor.ico"
)

Add-Type -AssemblyName System.Drawing

$srcPath = Join-Path $PSScriptRoot $Source
$outPath = Join-Path $PSScriptRoot $Output

if (-not (Test-Path $srcPath)) { Write-Error "Source not found: $srcPath"; exit 1 }

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$img = [System.Drawing.Image]::FromFile($srcPath)
[System.Drawing.Image]$srcImg = $img
$pngBlobs = New-Object System.Collections.ArrayList

foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode    = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode  = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.DrawImage($srcImg, 0, 0, $size, $size)
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    [void]$pngBlobs.Add($ms.ToArray())
}
$img.Dispose()

$fs = [System.IO.File]::Create($outPath)
$bw = New-Object System.IO.BinaryWriter($fs)

# ICONDIR
$bw.Write([uint16]0)                   # Reserved
$bw.Write([uint16]1)                   # Type = icon
$bw.Write([uint16]$sizes.Length)       # Image count

# ICONDIRENTRYs
$offset = 6 + (16 * $sizes.Length)
for ($i = 0; $i -lt $sizes.Length; $i++) {
    $size = $sizes[$i]
    $blob = $pngBlobs[$i]

    $w = if ($size -eq 256) { [byte]0 } else { [byte]$size }
    $h = if ($size -eq 256) { [byte]0 } else { [byte]$size }

    $bw.Write([byte]$w)
    $bw.Write([byte]$h)
    $bw.Write([byte]0)                 # ColorCount
    $bw.Write([byte]0)                 # Reserved
    $bw.Write([uint16]1)               # Planes
    $bw.Write([uint16]32)              # BitCount
    $bw.Write([uint32]$blob.Length)    # BytesInRes
    $bw.Write([uint32]$offset)         # ImageOffset

    $offset += $blob.Length
}

# Image data
foreach ($blob in $pngBlobs) {
    $bw.Write($blob)
}

$bw.Close()
$fs.Close()

Write-Host "Wrote $outPath ($($sizes.Length) sizes, $((Get-Item $outPath).Length) bytes)"
