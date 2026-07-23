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

$sourceSkills = Get-ChildItem -Path $sourceDir -Directory
$sourceNames = $sourceSkills | Select-Object -ExpandProperty Name

# Purge obsolete skills in target that no longer exist in source
$targetSkills = Get-ChildItem -Path $targetDir -Directory
foreach ($targetSkill in $targetSkills) {
    if ($targetSkill.Name -notin $sourceNames) {
        Write-Host "Removing obsolete target skill '$($targetSkill.Name)'..." -ForegroundColor Yellow
        Remove-Item -Path $targetSkill.FullName -Recurse -Force | Out-Null
    }
}

# Install/update source skills into target
foreach ($skill in $sourceSkills) {
    $destPath = Join-Path $targetDir $skill.Name
    Write-Host "Installing skill '$($skill.Name)'..." -ForegroundColor Cyan
    if (Test-Path $destPath) {
        Remove-Item -Path $destPath -Recurse -Force | Out-Null
    }
    Copy-Item -Path $skill.FullName -Destination $targetDir -Recurse -Force
}

Write-Host "All skills successfully installed to: $targetDir" -ForegroundColor Green
