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

if (Enter-InteractiveModeIfNoParameter -BoundParameters $PSBoundParameters -ScriptRoot $PSScriptRoot -NonInteractive:$NonInteractive) {
    return
}

$buildScriptPath = Join-Path $PSScriptRoot 'build.ps1'
& $buildScriptPath -Configuration $Configuration -NoRestore:$NoRestore -StopRunningInstance:$StopRunningInstance -WarnAsError
