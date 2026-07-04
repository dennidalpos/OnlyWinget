param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$StopRunningInstance,
    [switch]$DryRun,
    [switch]$All,
    [switch]$NuGetCache,
    [switch]$NonInteractive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/ScriptHelpers.ps1')

if (Enter-InteractiveModeIfNoParameter -BoundParameters $PSBoundParameters -ScriptRoot $PSScriptRoot -NonInteractive:$NonInteractive) {
    return
}

$repoRoot = Split-Path $PSScriptRoot -Parent
$generatedRootNames = @(
    'artifacts',
    'tmp',
    'build',
    'dist',
    'out',
    'publish',
    'coverage',
    'logs',
    'reports'
)
$generatedDirectoryNames = @(
    'bin',
    'obj',
    'TestResults'
)
$generatedFilePatterns = @(
    '*.binlog',
    '*.cache',
    '*.coverage',
    '*.log',
    '*.tmp',
    '*.trx'
)
$excludedTraversalRoots = @(
    (Join-Path $repoRoot '.git'),
    (Join-Path $repoRoot '.vs'),
    (Join-Path $repoRoot 'artifacts'),
    (Join-Path $repoRoot 'packages'),
    (Join-Path $repoRoot 'tools')
) | ForEach-Object {
    Get-NormalizedFullPath -Path $_
}

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

function Test-IsExcludedTraversalPath {
    param(
        [string]$Path
    )

    foreach ($excludedRoot in $excludedTraversalRoots) {
        if (Test-IsSameOrChildPath -Path $Path -ParentPath $excludedRoot) {
            return $true
        }
    }

    return $false
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

function Invoke-NuGetCacheClean {
    if (-not ($NuGetCache -or $All)) {
        return
    }

    if ($DryRun) {
        Write-Host '[dry-run] dotnet nuget locals global-packages --clear' -ForegroundColor Yellow
        Write-Host '[dry-run] dotnet nuget locals http-cache --clear' -ForegroundColor Yellow
        Write-Host '[dry-run] dotnet nuget locals temp --clear' -ForegroundColor Yellow
        Write-Host '[dry-run] dotnet nuget locals plugins-cache --clear' -ForegroundColor Yellow
        return
    }

    $failedCaches = [System.Collections.Generic.List[string]]::new()
    foreach ($cacheName in @('global-packages', 'http-cache', 'temp', 'plugins-cache')) {
        dotnet nuget locals $cacheName --clear
        if ($LASTEXITCODE -ne 0) {
            $failedCaches.Add($cacheName)
        }
    }

    if ($failedCaches.Count -gt 0) {
        Write-Warning "Pulizia cache NuGet/.NET parzialmente fallita per: $($failedCaches -join ', '). La clean del repository continua; chiudi Visual Studio/dotnet e riprova se vuoi svuotare anche quelle cache."
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

    $fullPath = Assert-RepositoryPathInAllowedRoot `
        -Path $Path `
        -RepositoryRoot $repoRoot `
        -AllowedRoots @($repoRoot) `
        -Description 'Pulizia'

    if (-not (Test-Path -LiteralPath $fullPath)) {
        return
    }

    if ($DryRun) {
        Write-Host "[dry-run] remove $fullPath" -ForegroundColor Yellow
        return
    }

    Remove-Item -LiteralPath $fullPath -Recurse -Force -ErrorAction Stop

    if (Test-Path -LiteralPath $fullPath) {
        $item = Get-Item -LiteralPath $fullPath -Force
        if ($item.PSIsContainer) {
            [System.IO.Directory]::Delete($fullPath, $true)
        }
        else {
            [System.IO.File]::Delete($fullPath)
        }
    }

    if (Test-Path -LiteralPath $fullPath) {
        throw "Impossibile rimuovere il percorso generato '$fullPath'."
    }
}

Invoke-ProjectClean -ProjectPaths $projectPaths

$safeTargets = @(
    $generatedRootNames | ForEach-Object {
        Join-Path $repoRoot $_
    }

    Get-ChildItem -Path $repoRoot -Directory -Recurse -Force |
        Where-Object {
            -not (Test-IsExcludedTraversalPath -Path $_.FullName) -and
            $_.Name -in $generatedDirectoryNames
        } |
        Select-Object -ExpandProperty FullName
)

foreach ($target in $safeTargets | Sort-Object -Unique) {
    Remove-GeneratedPath -Path $target
}

$generatedFiles = foreach ($pattern in $generatedFilePatterns) {
    Get-ChildItem -Path $repoRoot -Recurse -Force -File -Filter $pattern |
        Where-Object {
            -not (Test-IsExcludedTraversalPath -Path $_.FullName)
        } |
        Select-Object -ExpandProperty FullName
}

foreach ($target in $generatedFiles | Sort-Object -Unique) {
    Remove-GeneratedPath -Path $target
}

if ($All) {
    $aggressiveTargets = @(
        (Join-Path $repoRoot '.vs'),
        (Join-Path $repoRoot 'packages')
    )

    foreach ($target in $aggressiveTargets) {
        Remove-GeneratedPath -Path $target
    }

    # Pulizia profonda lato OS: preferenze e cache locali dell'app nel PC
    $localAppDataPath = Join-Path $env:LocalAppData 'OnlyWinget'
    if (Test-Path -LiteralPath $localAppDataPath) {
        if ($DryRun) {
            Write-Host "[dry-run] remove $localAppDataPath (AppData locale)" -ForegroundColor Yellow
        } else {
            Remove-Item -LiteralPath $localAppDataPath -Recurse -Force -ErrorAction SilentlyContinue
            if (Test-Path -LiteralPath $localAppDataPath) {
                try {
                    [System.IO.Directory]::Delete($localAppDataPath, $true)
                } catch {
                    Write-Warning "Impossibile rimuovere completamente la cartella AppData '$localAppDataPath': $_"
                }
            }
        }
    }
}

Invoke-NuGetCacheClean

$modeLabel = if ($All) { 'clean:all' } else { 'clean' }
Write-Host "Clean completata ($Configuration, $modeLabel)." -ForegroundColor Green
