# scripts/install-skills.ps1
# Installs the custom OnlyWinget developer skill into this workspace.
# Usage:
#   .\scripts\install-skills.ps1

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$skillName = 'onlywinget'
$sourcePath = Join-Path $PSScriptRoot '..\skills\onlywinget'
$targetPath = Join-Path $PSScriptRoot "..\.agents\skills\$skillName"

Write-Host 'Installing OnlyWinget skill in workspace...' -ForegroundColor Cyan

if (-not (Test-Path $sourcePath)) {
    throw "Source skill folder not found at: $sourcePath"
}

$parentDir = Split-Path $targetPath -Parent
if (-not (Test-Path $parentDir)) {
    New-Item -ItemType Directory -Path $parentDir -Force | Out-Null
}

if (Test-Path $targetPath) {
    Write-Host "Removing existing version at $targetPath..." -ForegroundColor Yellow
    Remove-Item -Path $targetPath -Recurse -Force | Out-Null
}

Write-Host "Copying skill directory to $targetPath..." -ForegroundColor Cyan
Copy-Item -Path $sourcePath -Destination $targetPath -Recurse -Force

Write-Host "Skill '$skillName' successfully installed to: $targetPath" -ForegroundColor Green
