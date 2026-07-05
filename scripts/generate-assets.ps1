param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NonInteractive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptRoot = Split-Path $MyInvocation.MyCommand.Path -Parent
. (Join-Path $scriptRoot 'support/ScriptHelpers.ps1')

$repoRoot = Split-Path $scriptRoot -Parent
$targetFramework = 'net10.0-windows10.0.17763.0'
$exePath = Join-Path $repoRoot "artifacts/bin/OnlyWinget/$Configuration/$targetFramework/win-x64/OnlyWinget.exe"

# ==========================================
# CENTRALIZED ASSET DISTRIBUTION & GENERATION
# ==========================================

$masterLogoPng = Join-Path $repoRoot 'assets/logos/logo.png'
$masterLogoIco = Join-Path $repoRoot 'assets/logos/logo.ico'
$masterWebKit = Join-Path $repoRoot 'assets/webkit.jpg'
$masterPoster = Join-Path $repoRoot 'assets/poster.jpg'

Write-Host "Distributing logos, web kit, and poster..." -ForegroundColor Cyan

# 1. Distribute brand logos
Copy-Item $masterLogoPng (Join-Path $repoRoot 'src/OnlyWinget/Assets/OnlyWinget-icon.png') -Force
Copy-Item $masterLogoPng (Join-Path $repoRoot 'landing/assets/logo.png') -Force
Copy-Item $masterLogoIco (Join-Path $repoRoot 'src/OnlyWinget/Assets/OnlyWinget.ico') -Force
Copy-Item $masterLogoIco (Join-Path $repoRoot 'landing/favicon.ico') -Force

# 2. Distribute web kit & poster
Copy-Item $masterWebKit (Join-Path $repoRoot 'landing/assets/webkit.jpg') -Force
Copy-Item $masterPoster (Join-Path $repoRoot 'landing/assets/poster.jpg') -Force

# 3. Generate WiX installer BMPs programmatically
function New-WixInstallerBmp {
    param(
        [string]$logoPngPath,
        [string]$setupAssetsDir
    )

    Write-Host "Generating WiX Installer BMPs..." -ForegroundColor Cyan
    Add-Type -AssemblyName System.Drawing

    $bannerPath = Join-Path $setupAssetsDir "WixUIBanner.bmp"
    $dialogPath = Join-Path $setupAssetsDir "WixUIDialog.bmp"

    # Ensure setup assets directory exists
    New-Item -ItemType Directory -Path $setupAssetsDir -Force | Out-Null

    # Load logo
    $logo = [System.Drawing.Image]::FromFile($logoPngPath)
    try {
        # 1. Generate WixUIBanner.bmp (493 x 58) as 24bpp BMP
        $banner = New-Object System.Drawing.Bitmap(493, 58, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
        $gBanner = [System.Drawing.Graphics]::FromImage($banner)
        try {
            $c1 = [System.Drawing.ColorTranslator]::FromHtml("#0E0A16")
            $c2 = [System.Drawing.ColorTranslator]::FromHtml("#1C122C")
            $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
                (New-Object System.Drawing.Rectangle(0, 0, 493, 58)),
                $c1, $c2, 0.0
            )
            $gBanner.FillRectangle($brush, 0, 0, 493, 58)
            $brush.Dispose()

            # Logo on the right side
            $gBanner.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $gBanner.DrawImage($logo, 430, 5, 48, 48)

            $banner.Save($bannerPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
            Write-Host "Generated WixUIBanner.bmp successfully." -ForegroundColor Green
        }
        finally {
            $gBanner.Dispose()
            $banner.Dispose()
        }

        # 2. Generate WixUIDialog.bmp (496 x 312) as 24bpp BMP
        $dialog = New-Object System.Drawing.Bitmap(496, 312, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
        $gDialog = [System.Drawing.Graphics]::FromImage($dialog)
        try {
            $gDialog.Clear([System.Drawing.Color]::White)

            # Left Sidebar Gradient
            $c1 = [System.Drawing.ColorTranslator]::FromHtml("#0E0A16")
            $c2 = [System.Drawing.ColorTranslator]::FromHtml("#1C122C")
            $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
                (New-Object System.Drawing.Rectangle(0, 0, 164, 312)),
                $c1, $c2, 90.0
            )
            $gDialog.FillRectangle($brush, 0, 0, 164, 312)
            $brush.Dispose()

            # Center logo inside sidebar
            $gDialog.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $gDialog.DrawImage($logo, 42, 60, 80, 80)

            $dialog.Save($dialogPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
            Write-Host "Generated WixUIDialog.bmp successfully." -ForegroundColor Green
        }
        finally {
            $gDialog.Dispose()
            $dialog.Dispose()
        }
    }
    finally {
        $logo.Dispose()
    }
}

New-WixInstallerBmp -logoPngPath $masterLogoPng -setupAssetsDir (Join-Path $repoRoot 'src/OnlyWinget.Setup/Assets')

# 1. Terminate any running instances of OnlyWinget
Write-Host "Killing any running OnlyWinget instances..." -ForegroundColor Cyan
Get-Process OnlyWinget -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

# Force rebuild to ensure the latest icons are compiled in
if (Test-Path $exePath) {
    Remove-Item $exePath -Force
}

# 2. Check executable
if (-not (Test-Path $exePath)) {
    Write-Host "Building executable to embed new icons..." -ForegroundColor Cyan
    & (Join-Path $scriptRoot 'build.ps1') -Configuration $Configuration -NoRestore
}
Assert-Path -Path $exePath -Description 'Built application executable'

# 3. Setup output directories
$assetsDir = Join-Path $repoRoot 'assets'
$galleryDir = Join-Path $repoRoot 'landing/assets/gallery'

$flows = @('create_preset', 'search_apps', 'install_apps', 'windows_update', 'sources', 'activity', 'settings')
foreach ($flow in $flows) {
    New-Item -ItemType Directory -Path (Join-Path $assetsDir $flow) -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $galleryDir $flow) -Force | Out-Null
}

Write-Host "Starting OnlyWinget application process..." -ForegroundColor Cyan
$process = Start-Process -FilePath $exePath -ArgumentList '--demo' -WorkingDirectory (Split-Path $exePath -Parent) -PassThru -WindowStyle Normal
$appPid = $process.Id

try {
    # Wait for the app to start and the window to render
    Start-Sleep -Seconds 4

    Assert-Command -Name 'winapp'
    
    # Add User32 helpers for resizing and focus
    Add-Type -TypeDefinition @'
    using System;
    using System.Runtime.InteropServices;
    public static class OnlyWingetUiTestNative {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, bool repaint);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(IntPtr hwnd);
    }
'@

    Add-Type -AssemblyName UIAutomationClient
    Add-Type -AssemblyName UIAutomationTypes
    Add-Type -AssemblyName System.Windows.Forms

    # 4. Find the window handle (with retries for robust launch timing)
    Write-Host "Locating OnlyWinget main window handle..." -ForegroundColor Cyan
    $hwnd = [IntPtr]::Zero
    for ($i = 0; $i -lt 10; $i++) {
        $window = winapp ui list-windows -a $appPid --json 2>$null | ConvertFrom-Json |
            Where-Object { $_.processId -eq $appPid -and $_.className -ne '#32770' } |
            Select-Object -First 1
        if ($null -ne $window) {
            $hwnd = [IntPtr]::new([int64]$window.hwnd)
            break
        }
        Start-Sleep -Seconds 1
    }

    if ($hwnd -eq [IntPtr]::Zero) {
        throw 'OnlyWinget main window not found.'
    }
    
    # Resize window to standard premium layout scaled for DPI (1280x800 effective)
    $dpi = [OnlyWingetUiTestNative]::GetDpiForWindow($hwnd)
    if ($dpi -eq 0) { $dpi = 96 }
    $scale = $dpi / 96.0
    $width = [int](1280 * $scale)
    $height = [int](800 * $scale)
    Write-Host "Resizing OnlyWinget window to $width x $height (DPI: $dpi, Scale: $scale)..." -ForegroundColor Cyan
    if (-not [OnlyWingetUiTestNative]::MoveWindow($hwnd, 50, 50, $width, $height, $true)) {
        throw 'Failed to resize window.'
    }
    Start-Sleep -Milliseconds 500

    # Wait for the shell navigation to be ready
    winapp ui wait-for 'RootNavigation' -a $appPid -t 5000 -q

    # Helper function to take screenshot and save it to both assets and landing gallery
    function Save-FlowScreenshot {
        param(
            [string]$Flow,
            [string]$Name
        )
        # Ensure window is visible and active in foreground before screenshot
        [OnlyWingetUiTestNative]::ShowWindow($hwnd, 9) | Out-Null # SW_RESTORE
        [OnlyWingetUiTestNative]::SetForegroundWindow($hwnd) | Out-Null
        Start-Sleep -Milliseconds 500
        
        $file = "$Name.png"
        $assetsPath = Join-Path $assetsDir "$Flow/$file"
        $galleryPath = Join-Path $galleryDir "$Flow/$file"
        
        Write-Host "Capturing screenshot: $Flow/$file" -ForegroundColor Yellow
        winapp ui screenshot -a $appPid -o $assetsPath -q
        
        if (-not (Test-Path $assetsPath)) {
            throw "Screenshot file was not generated: $assetsPath"
        }
        Copy-Item -Path $assetsPath -Destination $galleryPath -Force
    }

    function Send-KeysToControl {
        param(
            [string]$controlId,
            [string]$text
        )
        [OnlyWingetUiTestNative]::ShowWindow($hwnd, 9) | Out-Null
        [OnlyWingetUiTestNative]::SetForegroundWindow($hwnd) | Out-Null
        Start-Sleep -Milliseconds 200
        winapp ui focus $controlId -a $appPid -q
        Start-Sleep -Milliseconds 200
        [System.Windows.Forms.SendKeys]::SendWait($text)
        Start-Sleep -Milliseconds 200
    }



    # ==========================================
    # FLOW 1: How to Create a List of Apps (Presets)
    # ==========================================
    Write-Host "Starting Flow 1: Create Preset..." -ForegroundColor Green
    winapp ui invoke 'NavPackages' -a $appPid -q
    winapp ui invoke 'PackagesPresetTab' -a $appPid -q
    Save-FlowScreenshot -Flow 'create_preset' -Name '01_presets_empty'

    # Open Add Preset Flyout, type name, capture with flyout open, and then save
    winapp ui invoke 'AddPresetBtn' -a $appPid -q
    Start-Sleep -Milliseconds 400
    Send-KeysToControl 'PresetNameTextBox' 'DeveloperSuite'
    Save-FlowScreenshot -Flow 'create_preset' -Name '02_preset_created'
    winapp ui invoke 'SavePresetBtn' -a $appPid -q
    Start-Sleep -Milliseconds 400

    # Open Add Package Flyout and Add Visual Studio Code
    winapp ui invoke 'AddPackageBtn' -a $appPid -q
    Start-Sleep -Milliseconds 400
    Send-KeysToControl 'PresetPackageSourceTextBox' 'winget'
    Send-KeysToControl 'PresetPackageIdTextBox' 'Microsoft.VisualStudioCode'
    winapp ui invoke 'SavePackageBtn' -a $appPid -q
    Start-Sleep -Milliseconds 400

    # Add Git.Git
    winapp ui invoke 'AddPackageBtn' -a $appPid -q
    Start-Sleep -Milliseconds 400
    Send-KeysToControl 'PresetPackageIdTextBox' 'Git.Git'
    winapp ui invoke 'SavePackageBtn' -a $appPid -q
    Start-Sleep -Milliseconds 400

    # Add Google.Chrome
    winapp ui invoke 'AddPackageBtn' -a $appPid -q
    Start-Sleep -Milliseconds 400
    Send-KeysToControl 'PresetPackageIdTextBox' 'Google.Chrome'
    winapp ui invoke 'SavePackageBtn' -a $appPid -q
    Start-Sleep -Milliseconds 400

    # Add Microsoft.PowerToys
    winapp ui invoke 'AddPackageBtn' -a $appPid -q
    Start-Sleep -Milliseconds 400
    Send-KeysToControl 'PresetPackageIdTextBox' 'Microsoft.PowerToys'
    winapp ui invoke 'SavePackageBtn' -a $appPid -q
    Start-Sleep -Milliseconds 400

    Save-FlowScreenshot -Flow 'create_preset' -Name '03_package_added'


    # ==========================================
    # FLOW 2: How to Search Apps
    # ==========================================
    Write-Host "Starting Flow 2: Search Apps..." -ForegroundColor Green
    winapp ui invoke 'PackagesSearchTab' -a $appPid -q
    Send-KeysToControl 'PackageSearchQuery' 'vlc{ENTER}'
    
    Write-Host "Waiting for search results..." -ForegroundColor Cyan
    Start-Sleep -Seconds 5
    Save-FlowScreenshot -Flow 'search_apps' -Name '01_search_results'

    # Add the VLC result to the preset (select the row and click AddSelected)
    Send-KeysToControl 'SearchResults' ' '
    winapp ui invoke 'CommandAddSearchResults' -a $appPid -q
    Save-FlowScreenshot -Flow 'search_apps' -Name '02_search_added'

    # ==========================================
    # FLOW 3: How to Install Apps from Preset
    # ==========================================
    Write-Host "Starting Flow 3: Install Apps..." -ForegroundColor Green
    winapp ui invoke 'PackagesPresetTab' -a $appPid -q
    Save-FlowScreenshot -Flow 'install_apps' -Name '01_preset_ready'

    # Start installation
    winapp ui invoke 'CommandInstallPreset' -a $appPid -q
    Start-Sleep -Milliseconds 300
    Save-FlowScreenshot -Flow 'install_apps' -Name '02_install_progress'
    Start-Sleep -Seconds 2

    # ==========================================
    # FLOW 4: Windows Update
    # ==========================================
    Write-Host "Starting Flow 4: Windows Update..." -ForegroundColor Green
    winapp ui invoke 'NavUpdates' -a $appPid -q
    winapp ui invoke 'UpdatesWindowsTab' -a $appPid -q
    Save-FlowScreenshot -Flow 'windows_update' -Name '01_windows_update_tab'

    # Click scan updates and wait for the results to populate
    winapp ui invoke 'CommandScanWindowsUpdates' -a $appPid -q
    Write-Host "Waiting for update scan to finish..." -ForegroundColor Cyan
    Start-Sleep -Seconds 8
    Save-FlowScreenshot -Flow 'windows_update' -Name '02_scan_in_progress'

    # ==========================================
    # FLOW 5: Sources Page
    # ==========================================
    Write-Host "Starting Flow 5: Sources Page..." -ForegroundColor Green
    winapp ui invoke 'NavSources' -a $appPid -q
    Start-Sleep -Seconds 2
    Save-FlowScreenshot -Flow 'sources' -Name '01_sources_list'

    # ==========================================
    # FLOW 6: Activity Page
    # ==========================================
    Write-Host "Starting Flow 6: Activity Page..." -ForegroundColor Green
    winapp ui invoke 'NavActivity' -a $appPid -q
    Start-Sleep -Seconds 2
    Save-FlowScreenshot -Flow 'activity' -Name '01_activity_log'

    # ==========================================
    # FLOW 7: Settings Page
    # ==========================================
    Write-Host "Starting Flow 7: Settings Page..." -ForegroundColor Green
    winapp ui invoke 'SettingsItem' -a $appPid -q
    Start-Sleep -Seconds 2
    Save-FlowScreenshot -Flow 'settings' -Name '01_settings_tab'

    Write-Host "All flows automated and captured successfully!" -ForegroundColor Green
}
finally {
    Write-Host "Stopping OnlyWinget application..." -ForegroundColor Cyan
    if ($null -ne $appPid) {
        Stop-Process -Id $appPid -Force -ErrorAction SilentlyContinue
    }
}
