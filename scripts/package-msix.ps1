[CmdletBinding()]
param (
    [string]$Configuration = 'Release',
    [string]$Platform = 'x64',
    [switch]$NonInteractive
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

$outputDir = Join-Path (Join-Path $repoRoot 'artifacts') 'msix'
if (Test-Path $outputDir) {
    Remove-Item $outputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

$binDir = Join-Path $repoRoot "artifacts/bin/OnlyWinget/$Configuration/net10.0-windows10.0.17763.0/win-$Platform"

Write-Host "Building OnlyWinget for MSIX Packaging ($Configuration | $Platform)..." -ForegroundColor Cyan
dotnet build src/OnlyWinget/OnlyWinget.csproj -c $Configuration -r "win-$Platform" --no-self-contained -p:Platform=$Platform

if (-not (Test-Path $binDir)) {
    throw "Build output directory not found: $binDir"
}

Write-Host "Staging MSIX packaging assets in $outputDir..." -ForegroundColor Green
$appxManifestSrc = Join-Path $repoRoot 'src/OnlyWinget/Package.appxmanifest'
$appxManifestDst = Join-Path $outputDir 'Package.appxmanifest'

Copy-Item $appxManifestSrc $appxManifestDst -Force

$winappCli = Get-Command 'winapp' -ErrorAction SilentlyContinue
if ($null -ne $winappCli) {
    Write-Host "Invoking winapp CLI for MSIX packaging..." -ForegroundColor Cyan
    & winapp package $binDir --manifest $appxManifestSrc --quiet
} else {
    Write-Host "MSIX manifest and build assets staged successfully at $outputDir" -ForegroundColor Yellow
}

Write-Host "MSIX packaging setup complete." -ForegroundColor Green
