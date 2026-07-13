# scripts/install-skills.ps1
# Installs developer skills into this workspace from the root skills folder.
# Usage:
#   .\scripts\install-skills.ps1

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$sourceDir = Join-Path $PSScriptRoot '..\skills'
$targetDir = Join-Path $PSScriptRoot '..\.agents\skills'

Write-Host 'Installing developer skills in workspace...' -ForegroundColor Cyan

if (-not (Test-Path $sourceDir)) {
    throw "Source skills folder not found at: $sourceDir"
}

if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
}

$skills = Get-ChildItem -Path $sourceDir -Directory
foreach ($skill in $skills) {
    $destPath = Join-Path $targetDir $skill.Name
    Write-Host "Installing skill '$($skill.Name)'..." -ForegroundColor Cyan
    if (Test-Path $destPath) {
        Remove-Item -Path $destPath -Recurse -Force | Out-Null
    }
    Copy-Item -Path $skill.FullName -Destination $targetDir -Recurse -Force
}

Write-Host "All skills successfully installed to: $targetDir" -ForegroundColor Green
