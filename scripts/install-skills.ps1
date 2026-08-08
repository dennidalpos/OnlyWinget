# scripts/install-skills.ps1
# Verifies developer skills installed in this workspace under .agents/skills.
# Usage:
#   .\scripts\install-skills.ps1

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$rootSkillsDir = Join-Path $PSScriptRoot '..\skills'
$agentsSkillsDir = Join-Path $PSScriptRoot '..\.agents\skills'

Write-Host 'Verifying developer skills in workspace...' -ForegroundColor Cyan

if (-not (Test-Path $rootSkillsDir)) {
    New-Item -ItemType Directory -Path $rootSkillsDir -Force | Out-Null
}
if (-not (Test-Path $agentsSkillsDir)) {
    New-Item -ItemType Directory -Path $agentsSkillsDir -Force | Out-Null
}

# Sync root skills to .agents/skills if present
$rootSkills = Get-ChildItem -Path $rootSkillsDir -Directory
foreach ($skill in $rootSkills) {
    Write-Host "Skill present in /skills: '$($skill.Name)'" -ForegroundColor Green
    $dest = Join-Path $agentsSkillsDir $skill.Name
    if (-not (Test-Path $dest)) {
        Copy-Item -Path $skill.FullName -Destination $agentsSkillsDir -Recurse -Force
    }
}

$agentsSkills = Get-ChildItem -Path $agentsSkillsDir -Directory
Write-Host "All $($agentsSkills.Count) developer skills verified at: $rootSkillsDir & $agentsSkillsDir" -ForegroundColor Green
