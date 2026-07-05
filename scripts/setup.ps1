param(
    [switch]$ForceEvaluate,
    [switch]$NonInteractive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/ScriptHelpers.ps1')

$repoRoot = Split-Path $PSScriptRoot -Parent
$solutionPath = Join-Path $repoRoot 'OnlyWinget.sln'

Assert-Command -Name 'dotnet'
Assert-Path -Path $solutionPath -Description 'Solution'

$restoreArgs = @($solutionPath, '--locked-mode')
if ($ForceEvaluate) {
    $restoreArgs += '--force-evaluate'
}

dotnet restore @restoreArgs
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet restore fallito.'
}

Write-Host 'Repository setup completato.' -ForegroundColor Green
