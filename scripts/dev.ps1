param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Build,
    [switch]$NoRestore,
    [switch]$StopRunningInstance,
    [switch]$NonInteractive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/ScriptHelpers.ps1')

$repoRoot = Split-Path $PSScriptRoot -Parent
$targetFramework = 'net10.0-windows10.0.17763.0'
$exePath = Join-Path (Get-OutputRoot -RepositoryRoot $repoRoot) "bin/OnlyWinget/$Configuration/$targetFramework/win-x64/OnlyWinget.exe"

if ($Build) {
    $buildScriptPath = Join-Path $PSScriptRoot 'build.ps1'
    Assert-Path -Path $buildScriptPath -Description 'Build script'
    & $buildScriptPath -Configuration $Configuration -NoRestore:$NoRestore -StopRunningInstance:$StopRunningInstance
    if ((Test-Path Variable:\LASTEXITCODE) -and $LASTEXITCODE -ne 0) {
        throw 'Build failed before starting dev mode.'
    }
}

Assert-Path -Path $exePath -Description 'Built application executable'

Start-Process -FilePath $exePath -WorkingDirectory (Split-Path $exePath -Parent) -WindowStyle Normal
Write-Host "OnlyWinget started: $exePath" -ForegroundColor Green
