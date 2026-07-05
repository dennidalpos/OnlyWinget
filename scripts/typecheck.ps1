param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoRestore,
    [switch]$StopRunningInstance,
    [switch]$NonInteractive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/ScriptHelpers.ps1')

$buildScriptPath = Join-Path $PSScriptRoot 'build.ps1'
& $buildScriptPath -Configuration $Configuration -NoRestore:$NoRestore -StopRunningInstance:$StopRunningInstance -WarnAsError -NonInteractive:$NonInteractive
