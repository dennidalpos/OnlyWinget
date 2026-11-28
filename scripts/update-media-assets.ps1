param(
    [string]$sourceImagePath = (Join-Path $PSScriptRoot '../assets/logos/logo.png'),
    [string]$OutputRoot = (Split-Path $PSScriptRoot -Parent)
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.Drawing
if (-not (Test-Path -LiteralPath $sourceImagePath -PathType Leaf)) { throw "Source image not found: $sourceImagePath" }

function Write-MultiResIco {
    param(
        [System.Drawing.Bitmap]$sourceBitmap,
        [string]$outputPath,
        [int[]]$sizes = @(256, 128, 64, 48, 32, 16)
    )

    $pngStreams = @()
    foreach ($sz in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.DrawImage($sourceBitmap, 0, 0, $sz, $sz)
        $g.Dispose()

        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        $pngStreams += ,@($sz, $ms.ToArray())
        $ms.Dispose()
    }

    $fs = [System.IO.File]::Create($outputPath)
    $writer = New-Object System.IO.BinaryWriter($fs)

    # ICONDIR Header
    $writer.Write([UInt16]0) # Reserved
    $writer.Write([UInt16]1) # Type (1 = ICO)
    $writer.Write([UInt16]$sizes.Count) # Image count

    # Calculate offsets
    $offset = 6 + ($sizes.Count * 16)

    foreach ($item in $pngStreams) {
        $sz = $item[0]
        $bytes = $item[1]

        $wByte = if ($sz -ge 256) { 0 } else { [byte]$sz }
        $hByte = if ($sz -ge 256) { 0 } else { [byte]$sz }

        $writer.Write([byte]$wByte)          # Width
        $writer.Write([byte]$hByte)          # Height
        $writer.Write([byte]0)               # Color count
        $writer.Write([byte]0)               # Reserved
        $writer.Write([UInt16]1)             # Planes
        $writer.Write([UInt16]32)            # Bit count
        $writer.Write([UInt32]$bytes.Length) # Bytes in resource
        $writer.Write([UInt32]$offset)       # Image offset

        $offset += $bytes.Length
    }

    # Write PNG payloads
    foreach ($item in $pngStreams) {
        $bytes = $item[1]
        $writer.Write($bytes, 0, $bytes.Length)
    }

    $writer.Flush()
    $writer.Dispose()
    $fs.Dispose()
}

function New-InstallerBitmap {
    param([int]$Width, [int]$Height, [Drawing.Bitmap]$Logo, [switch]$Welcome)
    $bitmap = [Drawing.Bitmap]::new($Width, $Height, [Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $font = [Drawing.Font]::new('Segoe UI', $(if ($Welcome) { 14 } else { 10 }), [Drawing.FontStyle]::Bold)
    $ready = $false
    try {
        $graphics.Clear([Drawing.Color]::FromArgb(15, 23, 42))
        $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        if ($Welcome) {
            $graphics.DrawImage($Logo, 34, 64, 96, 96)
            $graphics.DrawString('OnlyWinget', $font, [Drawing.Brushes]::White, 24, 184)
        } else {
            $graphics.DrawImage($Logo, 6, 8, 40, 40)
            $graphics.DrawString('OnlyWinget', $font, [Drawing.Brushes]::White, 52, 19)
        }
        $ready = $true
        return $bitmap
    }
    finally {
        $graphics.Dispose(); $font.Dispose()
        if (-not $ready) { $bitmap.Dispose() }
    }
}

$logo = [Drawing.Bitmap]::FromFile($sourceImagePath)
$normalized = [Drawing.Bitmap]::new(512, 512)
try {
    $graphics = [Drawing.Graphics]::FromImage($normalized)
    try {
        $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.DrawImage($logo, 0, 0, 512, 512)
    } finally { $graphics.Dispose(); $logo.Dispose() }
    $masterDirectory = Join-Path $OutputRoot 'assets/logos'
    $appDirectory = Join-Path $OutputRoot 'src/OnlyWinget/Assets'
    $landingDirectory = Join-Path $OutputRoot 'landing/assets'
    $setupDirectory = Join-Path $OutputRoot 'src/OnlyWinget.Setup/Assets'
    New-Item -ItemType Directory -Path $masterDirectory, $appDirectory, $landingDirectory, $setupDirectory -Force | Out-Null
    $png = Join-Path $masterDirectory 'logo.png'
    $ico = Join-Path $masterDirectory 'logo.ico'
    $normalized.Save($png, [Drawing.Imaging.ImageFormat]::Png)
    Write-MultiResIco -sourceBitmap $normalized -outputPath $ico
    Copy-Item -LiteralPath $png -Destination (Join-Path $appDirectory 'OnlyWinget-icon.png') -Force
    Copy-Item -LiteralPath $png -Destination (Join-Path $landingDirectory 'logo.png') -Force
    Copy-Item -LiteralPath $ico -Destination (Join-Path $appDirectory 'OnlyWinget.ico') -Force
    $header = New-InstallerBitmap -Width 150 -Height 57 -Logo $normalized
    try { $header.Save((Join-Path $setupDirectory 'HeaderBanner.bmp'), [Drawing.Imaging.ImageFormat]::Bmp) }
    finally { $header.Dispose() }
    $welcome = New-InstallerBitmap -Width 164 -Height 314 -Logo $normalized -Welcome
    try { $welcome.Save((Join-Path $setupDirectory 'WelcomeDialog.bmp'), [Drawing.Imaging.ImageFormat]::Bmp) }
    finally { $welcome.Dispose() }
    Write-Host 'PASS: repository logo and consumed NSIS header/welcome assets generated.'
}
finally { $normalized.Dispose() }
