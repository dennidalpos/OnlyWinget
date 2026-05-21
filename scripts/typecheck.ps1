param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoRestore,
    [switch]$StopRunningInstance
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$buildScriptPath = Join-Path $PSScriptRoot 'build.ps1'
& $buildScriptPath -Configuration $Configuration -NoRestore:$NoRestore -StopRunningInstance:$StopRunningInstance -WarnAsError
