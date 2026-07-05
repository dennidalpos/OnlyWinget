param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$WindowsAppRuntimeInstallerPath = $env:ONLYWINGET_WINDOWS_APP_RUNTIME_INSTALLER,
    [switch]$NoRestore,
    [switch]$NonInteractive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path $PSScriptRoot -Parent
$packageScript = Join-Path $PSScriptRoot 'package.ps1'
$landingBuildDir = Join-Path $repoRoot 'landing/build'

Write-Host "Generazione del setup.exe in corso..." -ForegroundColor Cyan

# Invoke package.ps1 to build the unified setup
$packageParams = @{
    Configuration = $Configuration
    NoRestore = $NoRestore
    NonInteractive = $NonInteractive
}
if (-not [string]::IsNullOrWhiteSpace($WindowsAppRuntimeInstallerPath)) {
    $packageParams.WindowsAppRuntimeInstallerPath = $WindowsAppRuntimeInstallerPath
}

& $packageScript @packageParams
if ($LASTEXITCODE -ne 0) {
    throw "Il packaging di OnlyWinget e' fallito."
}

# Locate the generated setup file
$distDir = Join-Path $repoRoot "artifacts/dist/OnlyWinget/$Configuration"
$setupFile = Get-ChildItem -Path $distDir -Filter "*-setup.exe" | Select-Object -First 1
$portableFile = Get-ChildItem -Path $distDir -Filter "*-portable-x64.zip" | Select-Object -First 1

if ($null -eq $setupFile) {
    throw "Impossibile trovare il file setup.exe generato in '$distDir'."
}
if ($null -eq $portableFile) {
    throw "Impossibile trovare il file portable ZIP generato in '$distDir'."
}

# Ensure destination directory exists and is clean
if (Test-Path -LiteralPath $landingBuildDir) {
    Remove-Item -Path (Join-Path $landingBuildDir "*") -Force -ErrorAction SilentlyContinue
} else {
    New-Item -ItemType Directory -Path $landingBuildDir -Force | Out-Null
}

# Copy setup file preserving versioned name
$setupName = $setupFile.Name
$destinationPath = Join-Path $landingBuildDir $setupName
Write-Host "Copia di $($setupFile.FullName) in $destinationPath..." -ForegroundColor Cyan
Copy-Item -LiteralPath $setupFile.FullName -Destination $destinationPath -Force

# Copy portable file preserving versioned name
$portableName = $portableFile.Name
$portableDestPath = Join-Path $landingBuildDir $portableName
Write-Host "Copia di $($portableFile.FullName) in $portableDestPath..." -ForegroundColor Cyan
Copy-Item -LiteralPath $portableFile.FullName -Destination $portableDestPath -Force

# Update references in index.html dynamically
$htmlPath = Join-Path $repoRoot 'landing/index.html'
if (Test-Path -LiteralPath $htmlPath) {
    Write-Host "Aggiornamento dei link di download in $htmlPath..." -ForegroundColor Cyan
    $htmlContent = Get-Content -Path $htmlPath -Raw
    
    # Regex replacement for setup link
    $htmlContent = $htmlContent -replace 'href="build/OnlyWinget-[\d\.]+-setup\.exe"', "href=`"build/$setupName`""
    $htmlContent = $htmlContent -replace 'href="build/setup\.exe"', "href=`"build/$setupName`""
    
    # Regex replacement for portable link
    $htmlContent = $htmlContent -replace 'href="build/OnlyWinget-[\d\.]+-portable-x64\.zip"', "href=`"build/$portableName`""
    $htmlContent = $htmlContent -replace 'href="build/portable\.zip"', "href=`"build/$portableName`""
    
    Set-Content -Path $htmlPath -Value $htmlContent -NoNewline
}

Write-Host "Setup e file portable per la landing page generati con successo!" -ForegroundColor Green

