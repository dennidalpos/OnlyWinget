# Verify canonical repository skills; no copying or environment changes.

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$agentsSkillsDir = Join-Path $PSScriptRoot '..\.agents\skills'

Write-Host 'Verifying developer skills in workspace...' -ForegroundColor Cyan

if (-not (Test-Path -LiteralPath $agentsSkillsDir -PathType Container)) { throw 'Canonical .agents/skills directory is missing.' }
$agentsSkills = @(Get-ChildItem -LiteralPath $agentsSkillsDir -Directory)
if ($agentsSkills.Count -eq 0) { throw 'No repository skills found.' }
foreach ($skill in $agentsSkills) {
    $entry = Join-Path $skill.FullName 'SKILL.md'
    if (-not (Test-Path -LiteralPath $entry -PathType Leaf)) { throw "Skill entrypoint missing: $entry" }
}
Write-Host "PASS: $($agentsSkills.Count) canonical skill entrypoints verified in .agents/skills." -ForegroundColor Green
