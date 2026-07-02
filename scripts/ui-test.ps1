param(
    [Parameter(Mandatory)]
    [int]$AppPid,
    [string]$OutputDirectory,
    [switch]$CaptureAllRoutes,
    [switch]$NonInteractive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/ScriptHelpers.ps1')

$repoRoot = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts/ui-tests'
}

Assert-Command -Name 'winapp'
if ($null -eq (Get-Process -Id $AppPid -ErrorAction SilentlyContinue)) {
    throw "Processo OnlyWinget non trovato: $AppPid"
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$results = [System.Collections.Generic.List[object]]::new()
$pass = 0
$fail = 0

function Test-Ui {
    param([string]$Name, [scriptblock]$Action)

    try {
        & $Action
        if ($LASTEXITCODE -ne 0) {
            throw "Exit code $LASTEXITCODE"
        }

        $script:pass++
        $script:results.Add([pscustomobject]@{ name = $Name; status = 'PASS' })
    }
    catch {
        $script:fail++
        $script:results.Add([pscustomobject]@{ name = $Name; status = 'FAIL'; detail = $_.Exception.Message })
    }
}

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class OnlyWingetUiTestNative {
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, bool repaint);
    [DllImport("user32.dll")]
    public static extern void mouse_event(uint flags, uint dx, uint dy, int data, UIntPtr extraInfo);
    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);
}
'@

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

function Get-ScrollElement {
    param([string]$AutomationId)

    $root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $AutomationId)
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

$window = winapp ui list-windows -a $AppPid --json 2>$null | ConvertFrom-Json |
    Where-Object { $_.processId -eq $AppPid -and $_.className -ne '#32770' } |
    Select-Object -First 1
if ($null -eq $window) {
    throw 'Finestra principale OnlyWinget non trovata.'
}

$hwnd = [IntPtr]::new([int64]$window.hwnd)

Test-Ui 'Navigation shell is accessible' {
    winapp ui wait-for 'RootNavigation' -a $AppPid -t 5000 -q
    foreach ($navigationId in @('NavHome', 'NavPackages', 'NavUpdates', 'NavSources', 'NavActivity', 'SettingsItem')) {
        winapp ui wait-for $navigationId -a $AppPid -t 3000 -q
    }
}

Test-Ui 'Keyboard focus moves through navigation' {
    winapp ui focus 'RootNavigation' -a $AppPid -q
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.SendKeys]::SendWait('{TAB}')
    Start-Sleep -Milliseconds 200
    winapp ui get-focused -a $AppPid --json | Out-Null
}

Test-Ui 'Preset table exposes a scroll surface' {
    winapp ui invoke 'NavPackages' -a $AppPid -q
    winapp ui wait-for 'PresetPackageList' -a $AppPid -t 3000 -q
    $scrollElement = Get-ScrollElement -AutomationId 'PresetPackageList'
    if ($null -eq $scrollElement) {
        throw 'Tabella preset non trovata tramite UI Automation.'
    }

    $scrollElement.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern) | Out-Null
}

Test-Ui 'Source toggle can be changed and restored' {
    winapp ui invoke 'NavSources' -a $AppPid -q
    winapp ui wait-for 'SourceEnabledToggle' -a $AppPid -t 10000 -q
    winapp ui invoke 'SourceEnabledToggle' -a $AppPid -q
    winapp ui invoke 'SourceEnabledToggle' -a $AppPid -q
}

Test-Ui 'Import picker can be cancelled without mutation' {
    winapp ui invoke 'NavPackages' -a $AppPid -q
    winapp ui invoke 'MoreButton' -a $AppPid -q
    winapp ui wait-for 'CommandImportPreset' -a $AppPid -t 3000 -q
    winapp ui invoke 'CommandImportPreset' -a $AppPid -q
    Start-Sleep -Seconds 2
    $picker = winapp ui list-windows --json 2>$null | ConvertFrom-Json |
        Where-Object {
            $_.PSObject.Properties.Name -contains 'title' -and
            $_.PSObject.Properties.Name -contains 'ownerHwnd' -and
            $_.title -match 'Open|Apri' -and
            $_.ownerHwnd -eq $window.hwnd
        } |
        Select-Object -First 1
    if ($null -eq $picker) {
        throw 'File picker non trovato.'
    }

    $pickerTree = winapp ui inspect -w $picker.hwnd --interactive --json 2>$null | ConvertFrom-Json
    $cancel = $pickerTree.windows.elements |
        Where-Object { $_.type -eq 'Button' -and $_.name -match 'Cancel|Annulla' } |
        Select-Object -First 1
    if ($null -eq $cancel) {
        throw 'Pulsante di annullamento del file picker non trovato.'
    }

    winapp ui invoke $cancel.selector -w $picker.hwnd -q
}

Test-Ui 'Shared tables and progress controls expose accessibility metadata' {
    $tableXaml = Get-Content -Raw (Join-Path $repoRoot 'src/OnlyWinget/Controls/OnlyWingetTable.xaml')
    $tableCode = Get-Content -Raw (Join-Path $repoRoot 'src/OnlyWinget/Controls/OnlyWingetTable.xaml.cs')
    $presetXaml = Get-Content -Raw (Join-Path $repoRoot 'src/OnlyWinget/Features/Packages/PresetsPage.xaml')
    $updatesXaml = Get-Content -Raw (Join-Path $repoRoot 'src/OnlyWinget/Features/Updates/UpdatesPage.xaml')
    $bannerXaml = Get-Content -Raw (Join-Path $repoRoot 'src/OnlyWinget/DesignSystem/States/OperationBanner.xaml')
    if ($tableXaml -notmatch 'ListView' -or
        $tableCode -notmatch 'AutomationProperties.SetName' -or
        $tableCode -notmatch 'ListViewSelectionMode.Multiple' -or
        $presetXaml -notmatch 'OperationBanner' -or
        $updatesXaml -notmatch 'OperationBanner' -or
        $bannerXaml -notmatch 'AutomationProperties.LiveSetting="Polite"' -or
        $bannerXaml -notmatch 'ProgressBar') {
        throw 'Metadati di accessibilita del progresso incompleti.'
    }
}

foreach ($layout in @(
    @{ Name = 'compact'; Width = 640; Height = 720 },
    @{ Name = 'medium'; Width = 900; Height = 760 },
    @{ Name = 'wide'; Width = 1280; Height = 800 }
)) {
    Test-Ui "Layout $($layout.Name) renders" {
        if (-not [OnlyWingetUiTestNative]::MoveWindow($hwnd, 80, 80, $layout.Width, $layout.Height, $true)) {
            throw "MoveWindow fallito per $($layout.Name)."
        }

        Start-Sleep -Milliseconds 300
        winapp ui screenshot -a $AppPid -o (Join-Path $OutputDirectory "$($layout.Name).png") -q
    }
}

Test-Ui 'Interactive controls have AutomationId' {
    $inspection = winapp ui inspect -a $AppPid --interactive --json 2>$null | ConvertFrom-Json
    $elements = @($inspection.windows | ForEach-Object { $_.elements })
    $missing = @($elements | Where-Object {
        $_.type -match 'Button|TextBox|ComboBox|CheckBox|ToggleSwitch|NavigationViewItem' -and
        (-not ($_.PSObject.Properties.Name -contains 'name') -or
            $_.name -notmatch 'Minimize|Maximize|Close|System') -and
        (-not ($_.PSObject.Properties.Name -contains 'automationId') -or
            [string]::IsNullOrWhiteSpace($_.automationId))
    })
    if ($missing.Count -gt 0) {
        throw (($missing | ForEach-Object {
            $name = if ($_.PSObject.Properties.Name -contains 'name') { $_.name } else { '<unnamed>' }
            "$($_.type) '$name'"
        }) -join ', ')
    }
}

if ($CaptureAllRoutes) {
    $routeDirectory = Join-Path $OutputDirectory 'routes'
    New-Item -ItemType Directory -Path $routeDirectory -Force | Out-Null
    if (-not [OnlyWingetUiTestNative]::MoveWindow($hwnd, 20, 20, 1800, 950, $true)) {
        throw 'Impossibile impostare la finestra per gli screenshot delle route.'
    }

    function Save-RouteScreenshot {
        param([string]$Name)
        Start-Sleep -Milliseconds 500
        winapp ui screenshot -a $AppPid -o (Join-Path $routeDirectory "$Name.png") -q
    }

    winapp ui invoke 'NavHome' -a $AppPid -q
    Save-RouteScreenshot '01-home'

    winapp ui invoke 'NavPackages' -a $AppPid -q
    winapp ui invoke 'PackagesPresetTab' -a $AppPid -q
    Save-RouteScreenshot '02-packages-presets'

    winapp ui invoke 'PackagesSearchTab' -a $AppPid -q
    winapp ui focus 'PackageSearchQuery' -a $AppPid -q
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.SendKeys]::SendWait('^a')
    [System.Windows.Forms.SendKeys]::SendWait('vlc{ENTER}')
    Start-Sleep -Seconds 8
    Save-RouteScreenshot '03-packages-search-populated'

    winapp ui invoke 'NavUpdates' -a $AppPid -q
    winapp ui invoke 'UpdatesWingetTab' -a $AppPid -q
    winapp ui click 'CommandRefreshUpdates' -a $AppPid -q
    winapp ui wait-for 'CommandRefreshUpdates' -a $AppPid -p IsEnabled --value True -t 90000 -q
    Save-RouteScreenshot '04-updates-winget-populated'

    winapp ui invoke 'UpdatesWindowsTab' -a $AppPid -q
    winapp ui click 'CommandScanWindowsUpdates' -a $AppPid -q
    winapp ui wait-for 'CommandScanWindowsUpdates' -a $AppPid -p IsEnabled --value True -t 90000 -q
    Save-RouteScreenshot '05-updates-windows-populated'

    winapp ui invoke 'NavSources' -a $AppPid -q
    Start-Sleep -Seconds 3
    Save-RouteScreenshot '06-sources'

    winapp ui click 'NavActivity' -a $AppPid -q
    Start-Sleep -Seconds 2
    Save-RouteScreenshot '07-activity'

    winapp ui invoke 'SettingsItem' -a $AppPid -q
    Save-RouteScreenshot '08-settings'
}

$results | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $OutputDirectory 'results.json') -Encoding utf8
Write-Host "UI test: $pass passati, $fail falliti."
if ($fail -gt 0) {
    exit 1
}
