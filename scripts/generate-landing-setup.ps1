param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$NoRestore,
    [switch]$NonInteractive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/LandingArtifacts.ps1')
$repoRoot = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'package.ps1') -Configuration $Configuration -NoRestore:$NoRestore -NonInteractive:$NonInteractive
Copy-CurrentLandingArtifactPair -RepositoryRoot $repoRoot -Configuration $Configuration
