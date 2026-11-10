param(
    [switch]$Fix,
    [switch]$NoRestore,
    [switch]$NonInteractive,
    [switch]$Fast,
    [switch]$Full
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/ScriptHelpers.ps1')

$isFastMode = -not $Full

$repoRoot = Split-Path $PSScriptRoot -Parent
$solutionPath = Join-Path $repoRoot 'OnlyWinget.sln'

Assert-Command -Name 'dotnet'
Assert-Path -Path $solutionPath -Description 'Solution'

if (-not $NoRestore) {
    if ($isFastMode) {
        dotnet restore $solutionPath --locked-mode > $null
    } else {
        dotnet restore $solutionPath --locked-mode
    }
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet restore per il format fallito.'
    }
}

$formatArgs = @($solutionPath)
if (-not $Fix) {
    $formatArgs += '--verify-no-changes'
}
$formatArgs += '--no-restore'
if ($isFastMode) {
    $formatArgs += '--verbosity'
    $formatArgs += 'quiet'
}

if ($isFastMode) {
    dotnet format @formatArgs > $null
} else {
    dotnet format @formatArgs
}
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet format fallito.'
}

if ($Fix) {
    Write-Host 'PASS: Formattazione applicata.' -ForegroundColor Green
} else {
    Write-Host 'PASS: Verifica formato completata.' -ForegroundColor Green
}

