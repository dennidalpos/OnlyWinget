# scripts/install-skills.ps1
# Copies and installs the custom OnlyWinget developer skill.
# Usage:
#   .\scripts\install-skills.ps1           # Installs locally in the workspace .agents/skills folder
#   .\scripts\install-skills.ps1 -Global   # Installs globally in the user's .gemini/config folder

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$Global
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$skillName = "onlywinget"
$sourcePath = Join-Path $PSScriptRoot "..\skills\onlywinget"

# Determine target directory
if ($Global) {
    $configDir = Join-Path $env:USERPROFILE ".gemini\config\skills"
    $targetPath = Join-Path $configDir $skillName
    Write-Host "Installing OnlyWinget skill globally..." -ForegroundColor Cyan
} else {
    $targetPath = Join-Path $PSScriptRoot "..\.agents\skills\$skillName"
    Write-Host "Installing OnlyWinget skill in workspace..." -ForegroundColor Cyan
}

# Verify source exists
if (-not (Test-Path $sourcePath)) {
    throw "Source skill folder not found at: $sourcePath"
}

# Create target directory if it doesn't exist
$parentDir = Split-Path $targetPath -Parent
if (-not (Test-Path $parentDir)) {
    New-Item -ItemType Directory -Path $parentDir -Force | Out-Null
}

# Remove existing skill version at target path if it exists
if (Test-Path $targetPath) {
    Write-Host "Removing existing version at $targetPath..." -ForegroundColor Yellow
    Remove-Item -Path $targetPath -Recurse -Force | Out-Null
}

# Copy the skill directory
Write-Host "Copying skill directory to $targetPath..." -ForegroundColor Cyan
Copy-Item -Path $sourcePath -Destination $targetPath -Recurse -Force

Write-Host "Skill '$skillName' successfully installed to: $targetPath" -ForegroundColor Green
