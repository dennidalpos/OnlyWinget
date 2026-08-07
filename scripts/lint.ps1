param(
    [switch]$Required,
    [switch]$NonInteractive,
    [switch]$Fast,
    [switch]$Full
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/ScriptHelpers.ps1')

$isFastMode = -not $Full

$module = Install-PowerShellModuleIfMissing -Name 'PSScriptAnalyzer'
Import-Module $module.Path -ErrorAction Stop
$settingsPath = Join-Path $PSScriptRoot 'support/PSScriptAnalyzerSettings.psd1'
$scriptsRoot = $PSScriptRoot
$scripts = Get-ChildItem $scriptsRoot -Recurse -Filter '*.ps1'

$anyIssue = $false
$okCount = 0
foreach ($s in $scripts) {
    $issues = Invoke-ScriptAnalyzer -Path $s.FullName -Settings $settingsPath -Severity Error,Warning
    if ($issues) {
        $anyIssue = $true
        foreach ($i in $issues) {
            Write-Host "$($s.Name):$($i.Line): [$($i.Severity)] $($i.RuleName) - $($i.Message)" -ForegroundColor Red
        }
    } else {
        $okCount++
        if (-not $isFastMode) {
            Write-Host "$($s.Name): OK"
        }
    }
}
if (-not $anyIssue) {
    if ($isFastMode) {
        Write-Host "PASS: All scripts linted ($okCount scripts OK)." -ForegroundColor Green
    } else {
        Write-Host 'All scripts OK' -ForegroundColor Green
    }
}
if ($anyIssue) { exit 1 }

