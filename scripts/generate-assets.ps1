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

# 1. Terminate any running instances of OnlyWinget
Write-Host "Killing any running OnlyWinget instances..." -ForegroundColor Cyan
taskkill /f /im OnlyWinget.exe 2>$null | Out-Null
Start-Sleep -Seconds 1

# 2. Check executable
if (-not (Test-Path $exePath)) {
    Write-Host "Executable not found at $exePath. Building..." -ForegroundColor Cyan
    & (Join-Path $scriptRoot 'build.ps1') -Configuration $Configuration -NoRestore
}
Assert-Path -Path $exePath -Description 'Built application executable'

# 3. Setup output directories
$assetsDir = Join-Path $repoRoot 'assets'
$galleryDir = Join-Path $repoRoot 'landing/assets/gallery'

$flows = @('create_preset', 'search_apps', 'install_apps', 'windows_update')
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
    }
'@

    Add-Type -AssemblyName UIAutomationClient
    Add-Type -AssemblyName UIAutomationTypes
    Add-Type -AssemblyName System.Windows.Forms

    # 4. Find the window handle
    $window = winapp ui list-windows -a $appPid --json 2>$null | ConvertFrom-Json |
        Where-Object { $_.processId -eq $appPid -and $_.className -ne '#32770' } |
        Select-Object -First 1
    
    if ($null -eq $window) {
        throw 'OnlyWinget main window not found.'
    }

    $hwnd = [IntPtr]::new([int64]$window.hwnd)
    
    # Resize window to standard premium layout (1280x800)
    Write-Host "Resizing OnlyWinget window to 1280x800..." -ForegroundColor Cyan
    if (-not [OnlyWingetUiTestNative]::MoveWindow($hwnd, 50, 50, 1280, 800, $true)) {
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
        Start-Sleep -Milliseconds 800
        $file = "$Name.png"
        $assetsPath = Join-Path $assetsDir "$Flow/$file"
        $galleryPath = Join-Path $galleryDir "$Flow/$file"
        
        Write-Host "Capturing screenshot: $Flow/$file" -ForegroundColor Yellow
        winapp ui screenshot -a $appPid -o $assetsPath -q
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

    # Enter Preset Name and Add
    Send-KeysToControl 'PresetNameTextBox' 'DeveloperSuite'
    winapp ui invoke 'AddPresetBtn' -a $appPid -q
    Save-FlowScreenshot -Flow 'create_preset' -Name '02_preset_created'

    # Enter Package IDs
    Send-KeysToControl 'PresetPackageIdTextBox' 'Microsoft.VisualStudioCode'
    winapp ui invoke 'AddPackageBtn' -a $appPid -q

    Send-KeysToControl 'PresetPackageIdTextBox' '^aGit.Git'
    winapp ui invoke 'AddPackageBtn' -a $appPid -q

    Send-KeysToControl 'PresetPackageIdTextBox' '^aGoogle.Chrome'
    winapp ui invoke 'AddPackageBtn' -a $appPid -q

    Send-KeysToControl 'PresetPackageIdTextBox' '^aMicrosoft.PowerToys'
    winapp ui invoke 'AddPackageBtn' -a $appPid -q


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
    Start-Sleep -Seconds 2
    Save-FlowScreenshot -Flow 'install_apps' -Name '02_install_progress'

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


    Write-Host "All flows automated and captured successfully!" -ForegroundColor Green
}
finally {
    Write-Host "Stopping OnlyWinget application..." -ForegroundColor Cyan
    Stop-Process -Id $appPid -Force -ErrorAction SilentlyContinue
}
