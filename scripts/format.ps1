param(
    [switch]$Fix,
    [switch]$NoRestore,
    [switch]$NonInteractive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/ScriptHelpers.ps1')

if (Enter-InteractiveModeIfNoParameter -BoundParameters $PSBoundParameters -ScriptRoot $PSScriptRoot -NonInteractive:$NonInteractive) {
    return
}

$repoRoot = Split-Path $PSScriptRoot -Parent
$solutionPath = Join-Path $repoRoot 'OnlyWinget.sln'

Assert-Command -Name 'dotnet'
Assert-Path -Path $solutionPath -Description 'Solution'

$formatArgs = @($solutionPath)
if (-not $Fix) {
    $formatArgs += '--verify-no-changes'
}
if ($NoRestore) {
    $formatArgs += '--no-restore'
}

dotnet format @formatArgs
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet format fallito.'
}

if ($Fix) {
    Write-Host 'Formattazione applicata.' -ForegroundColor Green
} else {
    Write-Host 'Verifica formato completata.' -ForegroundColor Green
}
