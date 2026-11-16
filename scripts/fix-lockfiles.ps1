#Requires -Version 5.1
<#
.SYNOPSIS
    Rigenera i packages.lock.json di tutti i progetti della solution.

.DESCRIPTION
    Runs 'dotnet restore --force-evaluate' to resolve error NU1004
    ("Runtime identifiers del progetto modificati") causato da lock file
    non piu coerenti con lo stato attuale dei .csproj.
    Da usare ogniqualvolta 'dotnet restore --locked-mode' fallisce con NU1004.

.EXAMPLE
    .\scripts\fix-lockfiles.ps1
#>

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot    = Split-Path $PSScriptRoot -Parent
$solutionPath = Join-Path $repoRoot 'OnlyWinget.sln'

if (-not (Test-Path $solutionPath)) {
    throw "Solution not found: $solutionPath"
}

Write-Host '[fix-lockfiles] Regenerating packages.lock.json...' -ForegroundColor Cyan

dotnet restore $solutionPath --force-evaluate
if ($LASTEXITCODE -ne 0) {
    throw '[fix-lockfiles] dotnet restore --force-evaluate failed.'
}

Write-Host ''
Write-Host '[fix-lockfiles] Verifying --locked-mode...' -ForegroundColor Cyan

dotnet restore $solutionPath --locked-mode > $null
if ($LASTEXITCODE -ne 0) {
    throw '[fix-lockfiles] --locked-mode verification failed. Check the dependencies.'
}

Write-Host '[fix-lockfiles] OK - lock files updated and consistent.' -ForegroundColor Green
Write-Host ''
Write-Host 'Remember to commit the updated packages.lock.json files:' -ForegroundColor Yellow
Write-Host '  git add src/**/packages.lock.json tests/**/packages.lock.json' -ForegroundColor Yellow
Write-Host '  git commit -m "chore: regenerate packages.lock.json (NU1004)"' -ForegroundColor Yellow
