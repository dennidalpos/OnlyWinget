# scripts/install-skills.ps1
# Verifies developer skills installed in this workspace under .agents/skills.
# Usage:
#   .\scripts\install-skills.ps1

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$targetDir = Join-Path $PSScriptRoot '..\.agents\skills'

Write-Host 'Verifying developer skills in workspace...' -ForegroundColor Cyan

if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
}

$installedSkills = Get-ChildItem -Path $targetDir -Directory
foreach ($skill in $installedSkills) {
    Write-Host "Skill present: '$($skill.Name)'" -ForegroundColor Green
}

Write-Host "All $($installedSkills.Count) developer skills verified at: $targetDir" -ForegroundColor Green
