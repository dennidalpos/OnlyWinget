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

if (Enter-InteractiveModeIfNoParameter -BoundParameters $PSBoundParameters -ScriptRoot $PSScriptRoot -NonInteractive:$NonInteractive) {
    return
}

$repoRoot = Split-Path $PSScriptRoot -Parent
$targetFramework = 'net10.0-windows10.0.17763.0'
$exePath = Join-Path $repoRoot "artifacts/bin/OnlyWinget/$Configuration/$targetFramework/OnlyWinget.exe"

if ($Build) {
    $buildScriptPath = Join-Path $PSScriptRoot 'build.ps1'
    Assert-Path -Path $buildScriptPath -Description 'Build script'
    & $buildScriptPath -Configuration $Configuration -NoRestore:$NoRestore -StopRunningInstance:$StopRunningInstance
}

Assert-Path -Path $exePath -Description 'Built application executable'

Start-Process -FilePath $exePath -WorkingDirectory (Split-Path $exePath -Parent) -WindowStyle Normal
Write-Host "OnlyWinget avviato: $exePath" -ForegroundColor Green
