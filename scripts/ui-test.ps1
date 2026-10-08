param(
    [Parameter(Mandatory)]
    [int]$AppPid,
    [string]$OutputDirectory,
    [switch]$CaptureAllRoutes,
    [switch]$NonInteractive,
    [switch]$Fast,
    [switch]$Full
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/ScriptHelpers.ps1')
. (Join-Path $PSScriptRoot 'support/UiTestWindowHelpers.ps1')
. (Join-Path $PSScriptRoot 'support/UiTestStateHelpers.ps1')
. (Join-Path $PSScriptRoot 'support/VerificationHelpers.ps1')

function Invoke-UiCli {
    param([Parameter(ValueFromRemainingArguments)][string[]]$Arguments)
    Invoke-CheckedNativeCommand -Command 'winapp' -Arguments $Arguments
}

$isFastMode = -not $Full

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
        $appProcess = Get-Process -Id $AppPid -ErrorAction Stop
        if ($appProcess.HasExited) { throw 'Application exited during UI validation.' }

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

$windowsRaw = Invoke-UiCli ui list-windows -a $AppPid --json 2>$null
$windowsList = if (-not [string]::IsNullOrWhiteSpace($windowsRaw)) { $windowsRaw | ConvertFrom-Json } else { @() }
$window = @($windowsList) |
    Where-Object { $_ -and $_.PSObject.Properties['processId'] -and $_.processId -eq $AppPid -and $_.className -ne '#32770' } |
    Select-Object -First 1
if ($null -eq $window) {
    throw "Finestra principale OnlyWinget non trovata per PID $AppPid."
}

$hwnd = [IntPtr]::new([int64]$window.hwnd)

Test-Ui 'Navigation shell is accessible' {
    Invoke-UiCli ui wait-for 'RootNavigation' -a $AppPid -t 5000 -q
    foreach ($navigationId in @('NavHome', 'NavPackages', 'NavUpdates', 'NavSources', 'NavActivity', 'SettingsItem')) {
        Invoke-UiCli ui wait-for $navigationId -a $AppPid -t 3000 -q
    }
}

Test-Ui 'Keyboard focus moves through navigation' {
    Invoke-UiCli ui focus 'RootNavigation' -a $AppPid -q
    $before = [System.Windows.Automation.AutomationElement]::FocusedElement
    if ($null -eq $before -or $before.Current.ProcessId -ne $AppPid) { throw 'Focus is outside the test application.' }
    $beforeId = $before.GetRuntimeId() -join ','
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.SendKeys]::SendWait('{TAB}')
    Start-Sleep -Milliseconds 200
    $after = [System.Windows.Automation.AutomationElement]::FocusedElement
    if ($null -eq $after -or $after.Current.ProcessId -ne $AppPid -or ($after.GetRuntimeId() -join ',') -eq $beforeId) {
        throw 'Tab did not move focus within the test application.'
    }
}

Test-Ui 'Preset table exposes a scroll surface' {
    Invoke-UiCli ui invoke 'NavPackages' -a $AppPid -q
    Invoke-UiCli ui wait-for 'PresetPackageList' -a $AppPid -t 3000 -q
    $scrollElement = Get-ScrollElement -AutomationId 'PresetPackageList'
    if ($null -eq $scrollElement) {
        throw 'Tabella preset non trovata tramite UI Automation.'
    }

    $scrollElement.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern) | Out-Null
}

Test-Ui 'Source toggle can be changed and restored' {
    Invoke-UiCli ui invoke 'NavSources' -a $AppPid -q
    Invoke-UiCli ui wait-for 'SourceEnabledToggle' -a $AppPid -t 10000 -q
    $toggle = Get-ScrollElement -AutomationId 'SourceEnabledToggle'
    $pattern = $toggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    $original = $pattern.Current.ToggleState
    try {
        $pattern.Toggle()
        Start-Sleep -Milliseconds 500
        if ($pattern.Current.ToggleState -eq $original) { throw 'Source toggle did not change state.' }
    }
    finally {
        if ($pattern.Current.ToggleState -ne $original) { $pattern.Toggle() }
        Start-Sleep -Milliseconds 500
        if ($pattern.Current.ToggleState -ne $original) { throw 'Source toggle was not restored.' }
    }
}

Test-Ui 'Import picker can be cancelled without mutation' {
    Invoke-UiCli ui invoke 'NavPackages' -a $AppPid -q
    $presetList = Get-ScrollElement -AutomationId 'PresetPackageList'
    $beforeRows = Get-UiTestStateSnapshot -Root $presetList
    $beforePreset = Get-UiTestStateSnapshot -Root (Get-ScrollElement -AutomationId 'PresetSelector')
    $existingWindowsRaw = Invoke-UiCli ui list-windows --json
    $existingHandles = [System.Collections.Generic.HashSet[long]]::new()
    foreach ($existingWindow in @($existingWindowsRaw | ConvertFrom-Json)) {
        $existingHandles.Add([int64]@($existingWindow.hwnd)[0]) | Out-Null
    }
    Invoke-UiCli ui invoke 'ImportPresetBtn' -a $AppPid -q
    Start-Sleep -Seconds 2
    $pickerWindowsRaw = Invoke-UiCli ui list-windows --json
    $pickers = @($pickerWindowsRaw | ConvertFrom-Json |
        Where-Object {
            $_.title -match 'Open|Apri' -or $_.className -eq '#32770'
        })

    $closedPicker = $false
    $closedHandles = [System.Collections.Generic.HashSet[long]]::new()
    foreach ($p in $pickers) {
        if ($null -ne $p -and $null -ne $p.hwnd) {
            $hVal = [int64]@($p.hwnd)[0]
            $pHwnd = [IntPtr]::new($hVal)
            if (-not $existingHandles.Contains($hVal) -and
                (Close-UiTestOwnedDialog -WindowHandle $pHwnd -AppWindowHandle $hwnd -AppProcessId $AppPid)) {
                $closedPicker = $true
                $closedHandles.Add($hVal) | Out-Null
            }
        }
    }
    if (-not $closedPicker) { throw 'No new picker owned by the test app was found; no unrelated window was closed.' }
    Start-Sleep -Seconds 2
    $remainingWindows = @(Invoke-UiCli ui list-windows --json | ConvertFrom-Json)
    if ($remainingWindows | Where-Object { $closedHandles.Contains([int64]@($_.hwnd)[0]) }) { throw 'Cancelled picker is still open.' }
    $afterRows = Get-UiTestStateSnapshot -Root (Get-ScrollElement -AutomationId 'PresetPackageList')
    $afterPreset = Get-UiTestStateSnapshot -Root (Get-ScrollElement -AutomationId 'PresetSelector')
    if ($afterRows -cne $beforeRows -or $afterPreset -cne $beforePreset) {
        throw 'Preset rows, checkbox states or selected preset changed after cancelling import.'
    }
}

Test-Ui 'Shared tables and progress controls expose accessibility metadata' {
    $tableXaml = Get-Content -Raw (Join-Path $repoRoot 'src/OnlyWinget/Controls/OnlyWingetTable.xaml')
    $tableCode = Get-Content -Raw (Join-Path $repoRoot 'src/OnlyWinget/Controls/OnlyWingetTable.xaml.cs')
    $presetXaml = Get-Content -Raw (Join-Path $repoRoot 'src/OnlyWinget/Features/Packages/PresetsPage.xaml')
    $updatesXaml = Get-Content -Raw (Join-Path $repoRoot 'src/OnlyWinget/Features/Updates/UpdatesPage.xaml')
    $bannerXaml = Get-Content -Raw (Join-Path $repoRoot 'src/OnlyWinget/DesignSystem/States/StatePresenter.xaml')
    if ($tableXaml -notmatch 'ListView' -or
        $tableCode -notmatch 'AutomationProperties.SetName' -or
        $tableCode -notmatch 'ListViewSelectionMode.None' -or
        $presetXaml -notmatch 'StatePresenter' -or
        $updatesXaml -notmatch 'StatePresenter' -or
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
    Test-Ui "Layout $($layout.Name) exposes bounded navigation" {
        if (-not [OnlyWingetUiTestNative]::MoveWindow($hwnd, 80, 80, $layout.Width, $layout.Height, $true)) {
            throw "MoveWindow fallito per $($layout.Name)."
        }

        Start-Sleep -Milliseconds 300
        $rootBounds = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd).Current.BoundingRectangle
        $navigationBounds = (Get-ScrollElement -AutomationId 'RootNavigation').Current.BoundingRectangle
        if ($navigationBounds.IsEmpty -or $navigationBounds.Width -le 0 -or $navigationBounds.Height -le 0 -or
            $navigationBounds.Left -lt $rootBounds.Left -or $navigationBounds.Right -gt $rootBounds.Right -or
            $navigationBounds.Top -lt $rootBounds.Top -or $navigationBounds.Bottom -gt $rootBounds.Bottom) {
            throw 'Navigation bounds exceed the resized application window.'
        }
        Invoke-UiCli ui screenshot -a $AppPid -o (Join-Path $OutputDirectory "$($layout.Name).png") -q
    }
}

Test-Ui 'Interactive controls have AutomationId' {
    $inspection = Invoke-UiCli ui inspect -w $window.hwnd --interactive --json 2>$null | ConvertFrom-Json
    $targetHwndHex = "0x{0:X}" -f [int64]$window.hwnd
    $elements = @($inspection.windows | Where-Object { $_.hwnd -eq $window.hwnd -or $_.hwnd -eq $targetHwndHex } | ForEach-Object { $_.elements })
    if ($elements.Count -eq 0) {
        throw 'No interactive elements were returned for the verified application window.'
    }
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
        Invoke-UiCli ui screenshot -a $AppPid -o (Join-Path $routeDirectory "$Name.png") -q
    }

    Invoke-UiCli ui invoke 'NavHome' -a $AppPid -q
    Save-RouteScreenshot '01-home'

    Invoke-UiCli ui invoke 'NavPackages' -a $AppPid -q
    Invoke-UiCli ui invoke 'PackagesPresetTab' -a $AppPid -q
    Save-RouteScreenshot '02-packages-presets'

    Invoke-UiCli ui invoke 'PackagesSearchTab' -a $AppPid -q
    Invoke-UiCli ui focus 'PackageSearchQuery' -a $AppPid -q
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.SendKeys]::SendWait('^a')
    [System.Windows.Forms.SendKeys]::SendWait('vlc{ENTER}')
    Start-Sleep -Seconds 8
    Save-RouteScreenshot '03-packages-search-populated'

    Invoke-UiCli ui invoke 'NavUpdates' -a $AppPid -q
    Invoke-UiCli ui invoke 'UpdatesWingetTab' -a $AppPid -q
    Invoke-UiCli ui click 'CommandRefreshUpdates' -a $AppPid -q
    Invoke-UiCli ui wait-for 'CommandRefreshUpdates' -a $AppPid -p IsEnabled --value True -t 90000 -q
    Save-RouteScreenshot '04-updates-winget-populated'

    Invoke-UiCli ui invoke 'UpdatesWindowsTab' -a $AppPid -q
    Start-Sleep -Seconds 2
    Invoke-UiCli ui click 'CommandScanWindowsUpdates' -a $AppPid -q
    Invoke-UiCli ui wait-for 'CommandScanWindowsUpdates' -a $AppPid -p IsEnabled --value True -t 90000 -q
    Save-RouteScreenshot '05-updates-windows-populated'

    Invoke-UiCli ui invoke 'NavSources' -a $AppPid -q
    Start-Sleep -Seconds 3
    Save-RouteScreenshot '06-sources'

    Invoke-UiCli ui click 'NavActivity' -a $AppPid -q
    Start-Sleep -Seconds 2
    Save-RouteScreenshot '07-activity'

    Invoke-UiCli ui invoke 'SettingsItem' -a $AppPid -q
    Save-RouteScreenshot '08-settings'
}

$results | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $OutputDirectory 'results.json') -Encoding utf8
if ($isFastMode) {
    if ($fail -eq 0) {
        Write-Host "PASS: $pass UI tests passed." -ForegroundColor Green
    }
    else {
        Write-Host "FAIL: $fail UI tests failed ($pass passed)." -ForegroundColor Red
        foreach ($r in ($results | Where-Object { $_.status -eq 'FAIL' })) {
            Write-Host "FAIL: $($r.name) - $($r.detail)" -ForegroundColor Red
        }
    }
}
else {
    Write-Host "UI test: $pass passati, $fail falliti."
}
if ($fail -gt 0) {
    exit 1
}
