$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path $PSScriptRoot -Parent
$jpgLogoPath = "C:\Users\Utente\.gemini\antigravity\brain\432ca59d-b236-4373-8cf8-2a4a1ab118cd\onlywinget_logo_1783205350847.jpg"
$assetsLogoDir = Join-Path $repoRoot 'assets/logos'
New-Item -ItemType Directory -Path $assetsLogoDir -Force | Out-Null

$pngLogoPath = Join-Path $assetsLogoDir 'logo.png'
$icoLogoPath = Join-Path $assetsLogoDir 'logo.ico'

# 1. Convert JPEG to PNG using GDI+
Add-Type -AssemblyName System.Drawing
Write-Host "Converting JPEG brand logo to PNG..." -ForegroundColor Cyan
$bitmap = [System.Drawing.Bitmap]::FromFile($jpgLogoPath)
try {
    # Resize to standard high-res 256x256 for icons
    $resized = New-Object System.Drawing.Bitmap(256, 256)
    $g = [System.Drawing.Graphics]::FromImage($resized)
    try {
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.DrawImage($bitmap, 0, 0, 256, 256)
    }
    finally {
        $g.Dispose()
    }

    # Save PNG
    $resized.Save($pngLogoPath, [System.Drawing.Imaging.ImageFormat]::Png)
    
    # 2. Convert to standard Windows ICO format using native GDI+
    Write-Host "Creating standard Windows ICO..." -ForegroundColor Cyan
    $hIcon = $resized.GetHicon()
    try {
        $icon = [System.Drawing.Icon]::FromHandle($hIcon)
        $fs = New-Object System.IO.FileStream($icoLogoPath, [System.IO.FileMode]::Create)
        try {
            $icon.Save($fs)
        }
        finally {
            $fs.Dispose()
            $icon.Dispose()
        }
    }
    finally {
        try {
            $sig = '[DllImport("user32.dll", SetLastError = true)] public static extern bool DestroyIcon(IntPtr hIcon);'
            Add-Type -MemberDefinition $sig -Name "User32Icon" -Namespace "OnlyWingetIconNamespace" -ErrorAction SilentlyContinue
            $null = [OnlyWingetIconNamespace.User32Icon]::DestroyIcon($hIcon)
        }
        catch {
            $null = $_
        }
    }
    
    $resized.Dispose()
}
finally {
    $bitmap.Dispose()
}

# 3. Distribute assets to their destination folders
Write-Host "Distributing logos..." -ForegroundColor Cyan

# Landing assets
$landingAssetsDir = Join-Path $repoRoot 'landing/assets'
New-Item -ItemType Directory -Path $landingAssetsDir -Force | Out-Null
Copy-Item -Path $pngLogoPath -Destination (Join-Path $landingAssetsDir 'logo.png') -Force

# App assets
$appAssetsDir = Join-Path $repoRoot 'src/OnlyWinget/Assets'
Copy-Item -Path $pngLogoPath -Destination (Join-Path $appAssetsDir 'OnlyWinget-icon.png') -Force
Copy-Item -Path $icoLogoPath -Destination (Join-Path $appAssetsDir 'OnlyWinget.ico') -Force

Write-Host "Logos aligned successfully!" -ForegroundColor Green
