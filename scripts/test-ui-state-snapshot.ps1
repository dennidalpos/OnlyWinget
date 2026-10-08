$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/UiTestStateHelpers.ps1')
$fixtureRoot = Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts/ui-state-tests/' + [Guid]::NewGuid().ToString('N'))
$sourcePath = Join-Path $fixtureRoot 'Fixture.cs'
$exePath = Join-Path $fixtureRoot 'Fixture.exe'
$process = $null
New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
try {
    @'
using System;
using System.Windows;
using System.Windows.Controls;
class Program {
    [STAThread] static void Main() {
        var window = new Window { Title = "OnlyWinget state fixture", Width = 320, Height = 260 };
        var panel = new StackPanel();
        var check = new CheckBox { Content = "Include package" };
        var list = new ListBox { Height = 100 };
        list.Items.Add("First preset");
        list.Items.Add("Second preset");
        list.SelectedIndex = 0;
        var selection = new Button { Content = "Change selection" };
        selection.Click += (sender, args) => list.SelectedIndex = 1 - list.SelectedIndex;
        var combo = new ComboBox();
        combo.Items.Add("First preset");
        combo.Items.Add("Second preset");
        combo.SelectedIndex = 0;
        var comboSelection = new Button { Content = "Change collapsed preset" };
        comboSelection.Click += (sender, args) => combo.SelectedIndex = 1 - combo.SelectedIndex;
        panel.Children.Add(check);
        panel.Children.Add(list);
        panel.Children.Add(selection);
        panel.Children.Add(combo);
        panel.Children.Add(comboSelection);
        window.Content = panel;
        new Application().Run(window);
    }
}
'@ | Set-Content -LiteralPath $sourcePath -Encoding utf8
    $frameworkRoot = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
    $references = @('PresentationFramework.dll', 'PresentationCore.dll', 'WindowsBase.dll') |
        ForEach-Object { '/reference:' + (Join-Path $frameworkRoot ('WPF/' + $_)) }
    $references += '/reference:' + (Join-Path $frameworkRoot 'System.Xaml.dll')
    & (Join-Path $frameworkRoot 'csc.exe') /nologo /target:winexe @references "/out:$exePath" $sourcePath
    if ($LASTEXITCODE -ne 0) { throw 'UI state fixture compilation failed.' }
    $process = Start-Process -FilePath $exePath -WindowStyle Minimized -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(5)
    do {
        $process.Refresh()
        if ($process.HasExited) { throw 'UI state fixture exited before exposing a window.' }
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) { break }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($process.MainWindowHandle -eq [IntPtr]::Zero) { throw 'UI state fixture window unavailable.' }
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
    $baseline = Get-UiTestStateSnapshot -Root $root
    $baselineNames = @($baseline | ConvertFrom-Json | ForEach-Object name) -join [char]0
    if ((Get-UiTestStateSnapshot -Root $root) -cne $baseline) { throw 'Unchanged state produced different snapshots.' }
    foreach ($case in @(@{ Name='Include package'; Pattern=[System.Windows.Automation.TogglePattern]::Pattern },
        @{ Name='Change selection'; Pattern=[System.Windows.Automation.InvokePattern]::Pattern },
        @{ Name='Change collapsed preset'; Pattern=[System.Windows.Automation.InvokePattern]::Pattern })) {
        $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $case.Name)
        $element = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        $pattern = $element.GetCurrentPattern($case.Pattern)
        for ($step = 0; $step -lt 2; $step++) {
            if ($case.Name -eq 'Include package') { $pattern.Toggle() } else { $pattern.Invoke() }
            Start-Sleep -Milliseconds 200
            $snapshot = Get-UiTestStateSnapshot -Root $root
            if ($case.Name -ne 'Change collapsed preset' -and
                (@($snapshot | ConvertFrom-Json | ForEach-Object name) -join [char]0) -cne $baselineNames) {
                throw 'Fixture names changed; this would not reproduce the names-only comparison defect.'
            }
            if (($snapshot -ceq $baseline) -ne ($step -eq 1)) { throw "State mutation/restoration was not observed: $($case.Name)" }
        }
    }
    Write-Host 'PASS: real UIA snapshots detect checkbox/list/collapsed-preset selection mutations and match restored state.'
}
finally {
    if ($null -ne $process) {
        if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
        $process.Dispose()
    }
    foreach ($file in @($sourcePath, $exePath)) {
        if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file -Force }
    }
    Remove-Item -LiteralPath $fixtureRoot -ErrorAction Stop
}
