Param(
    [string]$sourceImagePath = "C:\Users\Utente\.gemini\antigravity\brain\335b98fa-6a7e-48ad-8c81-9e5857aefd9f\onlywinget_app_logo_1784720733052.jpg"
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $sourceImagePath)) {
    Write-Error "Source image not found: $sourceImagePath"
}

Write-Host "Processing master logo image..." -ForegroundColor Cyan
$srcBitmap = [System.Drawing.Bitmap]::FromFile($sourceImagePath)

# Crop central squircle logo tile (centered in 1024x1024)
$cropX = [int]($srcBitmap.Width * 0.19)
$cropY = [int]($srcBitmap.Height * 0.19)
$cropW = [int]($srcBitmap.Width * 0.62)
$cropH = [int]($srcBitmap.Height * 0.62)

$cropped = New-Object System.Drawing.Bitmap($cropW, $cropH)
$gCrop = [System.Drawing.Graphics]::FromImage($cropped)
$gCrop.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$gCrop.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$gCrop.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

$srcRect = New-Object System.Drawing.Rectangle($cropX, $cropY, $cropW, $cropH)
$destRect = New-Object System.Drawing.Rectangle(0, 0, $cropW, $cropH)
$gCrop.DrawImage($srcBitmap, $destRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
$gCrop.Dispose()
$srcBitmap.Dispose()

# Create high-res 512x512 logo.png
$logo512 = New-Object System.Drawing.Bitmap(512, 512)
$g512 = [System.Drawing.Graphics]::FromImage($logo512)
$g512.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g512.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g512.DrawImage($cropped, 0, 0, 512, 512)
$g512.Dispose()

# Directories
$assetsLogoDir = Join-Path $repoRoot 'assets/logos'
$landingAssetsDir = Join-Path $repoRoot 'landing/assets'
$appAssetsDir = Join-Path $repoRoot 'src/OnlyWinget/Assets'
$setupAssetsDir = Join-Path $repoRoot 'src/OnlyWinget.Setup/Assets'

New-Item -ItemType Directory -Path $assetsLogoDir -Force | Out-Null
New-Item -ItemType Directory -Path $landingAssetsDir -Force | Out-Null
New-Item -ItemType Directory -Path $appAssetsDir -Force | Out-Null
New-Item -ItemType Directory -Path $setupAssetsDir -Force | Out-Null

$masterPngPath = Join-Path $assetsLogoDir 'logo.png'
$masterIcoPath = Join-Path $assetsLogoDir 'logo.ico'

$logo512.Save($masterPngPath, [System.Drawing.Imaging.ImageFormat]::Png)

# Function to build a multi-resolution Windows ICO file with PNG frames
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

Write-Host "Generating multi-resolution ICO..." -ForegroundColor Cyan
Write-MultiResIco -sourceBitmap $logo512 -outputPath $masterIcoPath

# Copy logo PNG and ICO to targets
Copy-Item -Path $masterPngPath -Destination (Join-Path $landingAssetsDir 'logo.png') -Force
Copy-Item -Path $masterPngPath -Destination (Join-Path $appAssetsDir 'OnlyWinget-icon.png') -Force
Copy-Item -Path $masterIcoPath -Destination (Join-Path $appAssetsDir 'OnlyWinget.ico') -Force

# Generate Setup Banner BMP (493x58)
Write-Host "Generating WixUIBanner.bmp..." -ForegroundColor Cyan
$bannerWidth = 493
$bannerHeight = 58
$bannerBmp = New-Object System.Drawing.Bitmap($bannerWidth, $bannerHeight)
$gBanner = [System.Drawing.Graphics]::FromImage($bannerBmp)
$gBanner.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$gBanner.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

# Background gradient (dark indigo/slate)
$rectBanner = New-Object System.Drawing.Rectangle(0, 0, $bannerWidth, $bannerHeight)
$brushBg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    $rectBanner,
    [System.Drawing.Color]::FromArgb(255, 15, 23, 42),
    [System.Drawing.Color]::FromArgb(255, 30, 27, 75),
    [System.Drawing.Drawing2D.LinearGradientMode]::Horizontal
)
$gBanner.FillRectangle($brushBg, $rectBanner)

# Draw subtle ambient accent glow on the right
$glowPathBanner = New-Object System.Drawing.Drawing2D.GraphicsPath
$glowPathBanner.AddEllipse(350, -30, 160, 120)
$brushGlowBanner = New-Object System.Drawing.Drawing2D.PathGradientBrush($glowPathBanner)
$brushGlowBanner.CenterColor = [System.Drawing.Color]::FromArgb(70, 56, 189, 248)
$brushGlowBanner.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 15, 23, 42))
$gBanner.FillPath($brushGlowBanner, $glowPathBanner)
$brushGlowBanner.Dispose()
$glowPathBanner.Dispose()

# Draw icon on right side (42x42 at X=438, Y=8)
$gBanner.DrawImage($logo512, 438, 8, 42, 42)

# Draw Text branding
$fontTitle = New-Object System.Drawing.Font("Segoe UI", 12, [System.Drawing.FontStyle]::Bold)
$fontSub = New-Object System.Drawing.Font("Segoe UI", 8.5, [System.Drawing.FontStyle]::Regular)
$brushTextTitle = [System.Drawing.Brushes]::White
$brushTextSub = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 148, 163, 184))

$gBanner.DrawString("OnlyWinget", $fontTitle, $brushTextTitle, 16, 8)
$gBanner.DrawString("Package & Windows Update Manager", $fontSub, $brushTextSub, 16, 30)

$bannerBmp.Save((Join-Path $setupAssetsDir 'WixUIBanner.bmp'), [System.Drawing.Imaging.ImageFormat]::Bmp)

$gBanner.Dispose()
$bannerBmp.Dispose()
$brushBg.Dispose()

# Generate Setup Dialog BMP (493x314)
Write-Host "Generating WixUIDialog.bmp..." -ForegroundColor Cyan
$dialogWidth = 493
$dialogHeight = 314
$dialogBmp = New-Object System.Drawing.Bitmap($dialogWidth, $dialogHeight)
$gDialog = [System.Drawing.Graphics]::FromImage($dialogBmp)
$gDialog.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$gDialog.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

$rectDialog = New-Object System.Drawing.Rectangle(0, 0, $dialogWidth, $dialogHeight)
$brushBgDialog = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    $rectDialog,
    [System.Drawing.Color]::FromArgb(255, 11, 15, 25),
    [System.Drawing.Color]::FromArgb(255, 24, 28, 48),
    [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal
)
$gDialog.FillRectangle($brushBgDialog, $rectDialog)

# Ambient glow ring around central logo
$logoSize = 140
$logoX = [int](($dialogWidth - $logoSize) / 2)
$logoY = 48

$pathGlow = New-Object System.Drawing.Drawing2D.GraphicsPath
$pathGlow.AddEllipse(($logoX - 40), ($logoY - 40), ($logoSize + 80), ($logoSize + 80))
$pgb = New-Object System.Drawing.Drawing2D.PathGradientBrush($pathGlow)
$pgb.CenterColor = [System.Drawing.Color]::FromArgb(90, 99, 102, 241)
$pgb.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 11, 15, 25))
$gDialog.FillPath($pgb, $pathGlow)
$pgb.Dispose()
$pathGlow.Dispose()

# Draw Logo
$gDialog.DrawImage($logo512, $logoX, $logoY, $logoSize, $logoSize)

# Draw Title & Tagline under logo
$fontDialogTitle = New-Object System.Drawing.Font("Segoe UI", 20, [System.Drawing.FontStyle]::Bold)
$fontDialogSub = New-Object System.Drawing.Font("Segoe UI", 10.5, [System.Drawing.FontStyle]::Regular)
$brushTitle = [System.Drawing.Brushes]::White
$brushSub = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 165, 180, 205))

$sfCenter = New-Object System.Drawing.StringFormat
$sfCenter.Alignment = [System.Drawing.StringAlignment]::Center

$gDialog.DrawString("OnlyWinget", $fontDialogTitle, $brushTitle, ($dialogWidth / 2), 205, $sfCenter)
$gDialog.DrawString("Gestore Pacchetti e Aggiornamenti Windows", $fontDialogSub, $brushSub, ($dialogWidth / 2), 246, $sfCenter)

$dialogBmp.Save((Join-Path $setupAssetsDir 'WixUIDialog.bmp'), [System.Drawing.Imaging.ImageFormat]::Bmp)

$gDialog.Dispose()
$dialogBmp.Dispose()
$brushBgDialog.Dispose()
$logo512.Dispose()
$cropped.Dispose()

Write-Host "All media, setup, and app assets updated successfully!" -ForegroundColor Green
