param(
    [switch]$Required
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$lintScriptPath = Join-Path $PSScriptRoot 'lint-scripts.ps1'
& $lintScriptPath -Required:$Required
