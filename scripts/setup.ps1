param(
    [switch]$ForceEvaluate
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$installScriptPath = Join-Path $PSScriptRoot 'install.ps1'
& $installScriptPath -ForceEvaluate:$ForceEvaluate
