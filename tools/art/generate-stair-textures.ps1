$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$textureRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../1.6/Textures/Things/Building/DeepRimVertical'))
New-Item -ItemType Directory -Path $textureRoot -Force | Out-Null

foreach ($direction in @('Up', 'Down')) {
  $directionRoot = Join-Path $textureRoot ("Stairs$direction")
  New-Item -ItemType Directory -Path $directionRoot -Force | Out-Null
  foreach ($appearance in @('Smooth', 'Metal', 'Planks', 'Bricks')) {
    $bitmap = New-Object System.Drawing.Bitmap 128, 128
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    # Neutral sprites receive the real Stuff color from RimWorld's Graphic_Appearances.
    $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(48, 48, 48))
    try {
        $graphics.FillRectangle($brush, 8, 8, 112, 112)
        $brush.Color = [System.Drawing.Color]::FromArgb(240, 240, 240)
        $graphics.FillRectangle($brush, 12, 12, 104, 104)
        $brush.Color = [System.Drawing.Color]::FromArgb(30, 30, 30)
        $graphics.FillRectangle($brush, 22, 20, 76, 88)
        for ($step = 0; $step -lt 7; $step++) {
            $shade = if ($direction -eq 'Up') { 185 + $step * 9 } else { 239 - $step * 9 }
            $brush.Color = [System.Drawing.Color]::FromArgb($shade, $shade, $shade)
            $graphics.FillRectangle($brush, 27, (25 + $step * 11), 66, 8)
            $brush.Color = [System.Drawing.Color]::FromArgb(255, 255, 255)
            $graphics.FillRectangle($brush, 27, (25 + $step * 11), 66, 2)
            if ($appearance -eq 'Planks') {
                $brush.Color = [System.Drawing.Color]::FromArgb(125, 125, 125)
                $graphics.FillRectangle($brush, (31 + ($step % 3) * 4), (29 + $step * 11), 48, 1)
                $graphics.FillRectangle($brush, (45 + ($step % 2) * 5), (31 + $step * 11), 35, 1)
            } elseif ($appearance -eq 'Bricks') {
                $brush.Color = [System.Drawing.Color]::FromArgb(95, 95, 95)
                $offset = ($step % 2) * 10
                $graphics.FillRectangle($brush, (40 + $offset), (27 + $step * 11), 2, 6)
                $graphics.FillRectangle($brush, (65 + $offset), (27 + $step * 11), 2, 6)
            } elseif ($appearance -eq 'Metal') {
                $brush.Color = [System.Drawing.Color]::FromArgb(115, 115, 115)
                $graphics.FillRectangle($brush, 31, (29 + $step * 11), 2, 2)
                $graphics.FillRectangle($brush, 87, (29 + $step * 11), 2, 2)
            }
        }
        if ($appearance -eq 'Planks') {
            $brush.Color = [System.Drawing.Color]::FromArgb(130, 130, 130)
            $graphics.FillRectangle($brush, 16, 18, 1, 90)
            $graphics.FillRectangle($brush, 111, 18, 1, 90)
        } elseif ($appearance -eq 'Bricks') {
            $brush.Color = [System.Drawing.Color]::FromArgb(90, 90, 90)
            foreach ($joint in @(38, 65, 92)) {
                $graphics.FillRectangle($brush, 12, $joint, 10, 2)
                $graphics.FillRectangle($brush, 98, $joint, 18, 2)
            }
        } elseif ($appearance -eq 'Metal') {
            $brush.Color = [System.Drawing.Color]::FromArgb(110, 110, 110)
            foreach ($rivetX in @(14, 109)) {
                foreach ($rivetY in @(14, 109)) {
                    $graphics.FillEllipse($brush, $rivetX, $rivetY, 5, 5)
                }
            }
        }
        $brush.Color = [System.Drawing.Color]::White
        $graphics.FillRectangle($brush, 103, 38, 6, 50)
        [System.Drawing.Point[]]$arrow = if ($direction -eq 'Up') {
            @([System.Drawing.Point]::new(106, 22), [System.Drawing.Point]::new(96, 40), [System.Drawing.Point]::new(116, 40))
        } else {
            @([System.Drawing.Point]::new(106, 105), [System.Drawing.Point]::new(96, 87), [System.Drawing.Point]::new(116, 87))
        }
        $graphics.FillPolygon($brush, $arrow)
        $bitmap.Save((Join-Path $directionRoot ("Stairs${direction}_$appearance.png")), [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $brush.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
    }
  }
}
