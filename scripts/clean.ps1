param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$StopRunningInstance,
    [switch]$DryRun,
    [switch]$All
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'internal/ScriptHelpers.ps1')

$repoRoot = Split-Path $PSScriptRoot -Parent
$artifactsPath = Join-Path $repoRoot 'artifacts'
$tmpPath = Join-Path $repoRoot 'tmp'

function Get-SupportedProjectPath {
    @(
        Get-ChildItem -Path $repoRoot -Recurse -File -Filter '*.csproj' |
            Where-Object {
                $_.FullName -notmatch '\\(\.git|\.vs|artifacts|packages)\\'
            } |
            Sort-Object FullName |
            Select-Object -ExpandProperty FullName
    )
}

function Invoke-ProjectClean {
    param(
        [string[]]$ProjectPaths
    )

    foreach ($projectPath in $ProjectPaths) {
        if ($DryRun) {
            Write-Host "[dry-run] dotnet clean $projectPath -c $Configuration" -ForegroundColor Yellow
            continue
        }

        dotnet clean $projectPath -c $Configuration
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet clean fallito per il progetto '$projectPath'."
        }
    }
}

Assert-Command -Name 'dotnet'

$projectPaths = Get-SupportedProjectPath
if ($projectPaths.Count -eq 0) {
    throw "Nessun progetto .csproj trovato sotto '$repoRoot'."
}

if (-not $DryRun) {
    Assert-ExecutableNotLocked -KillProcess:$StopRunningInstance -ActionName 'Clean'
}

function Remove-GeneratedPath {
    param(
        [string]$Path
    )

    if (-not (Test-Path $Path)) {
        return
    }

    if ($DryRun) {
        Write-Host "[dry-run] remove $Path" -ForegroundColor Yellow
        return
    }

    Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop

    if (Test-Path $Path) {
        [System.IO.Directory]::Delete($Path, $true)
    }

    if (Test-Path $Path) {
        throw "Impossibile rimuovere il percorso generato '$Path'."
    }
}

Invoke-ProjectClean -ProjectPaths $projectPaths

$safeTargets = @(
    Get-ChildItem -Path $repoRoot -Directory -Recurse -Force |
        Where-Object {
            $_.FullName -notmatch '\\(\.git|\.vs)\\' -and
            $_.FullName -notlike "$artifactsPath*" -and
            $_.Name -in @('bin', 'obj', 'TestResults')
        } |
        Select-Object -ExpandProperty FullName
)

foreach ($target in $safeTargets | Sort-Object -Unique) {
    Remove-GeneratedPath -Path $target
}

Remove-GeneratedPath -Path $artifactsPath
Remove-GeneratedPath -Path $tmpPath

if ($All) {
    $aggressiveTargets = @(
        (Join-Path $repoRoot '.vs'),
        (Join-Path $repoRoot 'packages')
    )

    foreach ($target in $aggressiveTargets) {
        Remove-GeneratedPath -Path $target
    }
}

$modeLabel = if ($All) { 'clean:all' } else { 'clean' }
Write-Host "Clean completata ($Configuration, $modeLabel)." -ForegroundColor Green
