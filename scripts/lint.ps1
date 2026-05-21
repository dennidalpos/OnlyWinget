param(
    [switch]$Required
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$module = Get-Module -ListAvailable -Name PSScriptAnalyzer |
    Sort-Object Version -Descending |
    Select-Object -First 1
if ($null -eq $module) {
    Write-Warning 'PowerShell script lint not_available: PSScriptAnalyzer is not installed. Install it with: Install-Module PSScriptAnalyzer -Scope CurrentUser -Repository PSGallery'
    if ($Required) {
        exit 2
    }

    return
}

Import-Module $module.Path -ErrorAction Stop
$settingsPath = Join-Path $PSScriptRoot 'support/PSScriptAnalyzerSettings.psd1'
$scriptsRoot = $PSScriptRoot
$scripts = Get-ChildItem $scriptsRoot -Recurse -Filter '*.ps1' |
    Where-Object { $_.FullName -notmatch '\\legacy\\' }

$anyIssue = $false
foreach ($s in $scripts) {
    $issues = Invoke-ScriptAnalyzer -Path $s.FullName -Settings $settingsPath -Severity Error,Warning
    if ($issues) {
        $anyIssue = $true
        foreach ($i in $issues) {
            Write-Host "$($s.Name):$($i.Line): [$($i.Severity)] $($i.RuleName) - $($i.Message)"
        }
    } else {
        Write-Host "$($s.Name): OK"
    }
}
if (-not $anyIssue) { Write-Host 'All scripts OK' -ForegroundColor Green }
if ($anyIssue) { exit 1 }
