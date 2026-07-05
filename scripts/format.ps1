param(
    [switch]$Fix,
    [switch]$NoRestore,
    [switch]$NonInteractive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/ScriptHelpers.ps1')

$repoRoot = Split-Path $PSScriptRoot -Parent
$solutionPath = Join-Path $repoRoot 'OnlyWinget.sln'

Assert-Command -Name 'dotnet'
Assert-Path -Path $solutionPath -Description 'Solution'

if (-not $NoRestore) {
    dotnet restore $solutionPath -r win-x64 --locked-mode
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet restore per il format fallito.'
    }
}

$formatArgs = @($solutionPath)
if (-not $Fix) {
    $formatArgs += '--verify-no-changes'
}
$formatArgs += '--no-restore'

dotnet format @formatArgs
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet format fallito.'
}

if ($Fix) {
    Write-Host 'Formattazione applicata.' -ForegroundColor Green
} else {
    Write-Host 'Verifica formato completata.' -ForegroundColor Green
}
