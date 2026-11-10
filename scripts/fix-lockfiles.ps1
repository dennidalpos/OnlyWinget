#Requires -Version 5.1
<#
.SYNOPSIS
    Rigenera i packages.lock.json di tutti i progetti della solution.

.DESCRIPTION
    Esegue 'dotnet restore --force-evaluate' per risolvere l'errore NU1004
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
    throw "Solution non trovata: $solutionPath"
}

Write-Host '[fix-lockfiles] Rigenerazione packages.lock.json in corso...' -ForegroundColor Cyan

dotnet restore $solutionPath --force-evaluate
if ($LASTEXITCODE -ne 0) {
    throw '[fix-lockfiles] dotnet restore --force-evaluate fallito.'
}

Write-Host ''
Write-Host '[fix-lockfiles] Verifica --locked-mode...' -ForegroundColor Cyan

dotnet restore $solutionPath --locked-mode > $null
if ($LASTEXITCODE -ne 0) {
    throw '[fix-lockfiles] Verifica --locked-mode fallita. Controlla le dipendenze.'
}

Write-Host '[fix-lockfiles] OK - lock file aggiornati e coerenti.' -ForegroundColor Green
Write-Host ''
Write-Host 'Ricorda di committare i packages.lock.json aggiornati:' -ForegroundColor Yellow
Write-Host '  git add src/**/packages.lock.json tests/**/packages.lock.json' -ForegroundColor Yellow
Write-Host '  git commit -m "chore: rigenera packages.lock.json (NU1004)"' -ForegroundColor Yellow
