# app\app.ico 생성 (PNG 항목을 담은 다중 크기 ICO)
Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot '..\app\app.ico'
$sizes = 16, 20, 24, 32, 40, 48, 64, 256

function Draw([int]$n) {
    $bmp = New-Object System.Drawing.Bitmap $n, $n
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $s = $n / 256.0
    # 둥근 사각형 배경
    $r = 56 * $s; $m = 8 * $s; $w = $n - 2 * $m
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($m, $m, 2 * $r, 2 * $r, 180, 90)
    $path.AddArc($m + $w - 2 * $r, $m, 2 * $r, 2 * $r, 270, 90)
    $path.AddArc($m + $w - 2 * $r, $m + $w - 2 * $r, 2 * $r, 2 * $r, 0, 90)
    $path.AddArc($m, $m + $w - 2 * $r, 2 * $r, 2 * $r, 90, 90)
    $path.CloseFigure()
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new(0, 0)), ([System.Drawing.PointF]::new(0, $n)), ([System.Drawing.Color]::FromArgb(52, 56, 64)), ([System.Drawing.Color]::FromArgb(24, 26, 30))
    $g.FillPath($bg, $path)
    # 막대 3개: GPU(초록) / CPU(파랑) / GPU(초록)
    $bars = @(
        @{ x = 52;  h = 0.50; c = [System.Drawing.Color]::FromArgb(118, 185, 0) },
        @{ x = 108; h = 0.80; c = [System.Drawing.Color]::FromArgb(80, 160, 255) },
        @{ x = 164; h = 0.62; c = [System.Drawing.Color]::FromArgb(118, 185, 0) }
    )
    $base = 204; $top = 52
    foreach ($b in $bars) {
        $bh = ($base - $top) * $b.h
        $br = New-Object System.Drawing.SolidBrush $b.c
        $g.FillRectangle($br, [float]($b.x * $s), [float](($base - $bh) * $s), [float](40 * $s), [float]($bh * $s))
        $br.Dispose()
    }
    $g.Dispose()
    return $bmp
}

$pngs = foreach ($n in $sizes) {
    $bmp = Draw $n
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $ms.ToArray()
}

$fs = [System.IO.File]::Create($out)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $n = $sizes[$i]; $len = $pngs[$i].Length
    $bw.Write([byte]($n % 256)); $bw.Write([byte]($n % 256)); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]$len); $bw.Write([uint32]$offset)
    $offset += $len
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Close()
"wrote $out"
