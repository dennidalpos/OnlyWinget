param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Build,
    [switch]$NoRestore,
    [switch]$StopRunningInstance
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$runScriptPath = Join-Path $PSScriptRoot 'run.ps1'
& $runScriptPath -Configuration $Configuration -Build:$Build -NoRestore:$NoRestore -StopRunningInstance:$StopRunningInstance
