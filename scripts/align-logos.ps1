Param(
    [Parameter(Position = 0, Mandatory = $false)]
    [string]$jpgLogoPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$updateScriptPath = Join-Path $PSScriptRoot 'update-media-assets.ps1'

if (-not [string]::IsNullOrWhiteSpace($jpgLogoPath)) {
    if (-not (Test-Path $jpgLogoPath)) {
        Write-Error "The specified logo file does not exist: $jpgLogoPath"
        return
    }
    & $updateScriptPath -sourceImagePath (Resolve-Path $jpgLogoPath).Path
} else {
    & $updateScriptPath
}

Write-Host "Logos and media assets aligned successfully!" -ForegroundColor Green
