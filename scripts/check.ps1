param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$RunWingetSmoke
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$gateScriptPath = Join-Path $PSScriptRoot 'gate.ps1'
& $gateScriptPath -Configuration $Configuration -RunWingetSmoke:$RunWingetSmoke
