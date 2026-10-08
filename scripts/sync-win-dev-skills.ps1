# Check the maintained upstream reference without overwriting repository skills.

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoUrl = 'https://github.com/microsoft/winappCli.git'
$revision = @(git ls-remote --exit-code $repoUrl HEAD)
if ($LASTEXITCODE -ne 0 -or $revision.Count -ne 1) {
    throw 'Unable to resolve the maintained WinUI skills upstream HEAD.'
}

& (Join-Path $PSScriptRoot 'install-skills.ps1')
Write-Host "Upstream HEAD: $($revision[0].Split()[0])" -ForegroundColor Cyan
Write-Host 'Review WinUI skill updates at https://github.com/microsoft/winappCli before applying them manually.'
Write-Host 'Repository skills were preserved; upstream files were not imported.' -ForegroundColor Green
