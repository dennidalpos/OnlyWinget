$script:OutputRootVariable = 'ONLYWINGET_OUTPUT_ROOT'
$script:OutputRootMarker = '.onlywinget-output-root'

function Get-OutputRootOverride {
    $value = [Environment]::GetEnvironmentVariable($script:OutputRootVariable)
    if ([string]::IsNullOrWhiteSpace($value)) { return $null }
    return $value
}

# Single source of truth for managed build, package, test and fixture output.
# Default: <repository>/artifacts. Override: ONLYWINGET_OUTPUT_ROOT (absolute path).
function Get-OutputRoot {
    param([Parameter(Mandatory)][string]$RepositoryRoot)

    $override = Get-OutputRootOverride
    $repository = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\', '/')
    if ($null -eq $override) { return (Join-Path $repository 'artifacts') }

    if (-not [IO.Path]::IsPathRooted($override) -or $override -notmatch '^(?:[A-Za-z]:[\\/]|\\\\)') {
        throw "$($script:OutputRootVariable) must be an absolute path: $override"
    }
    $root = [IO.Path]::GetFullPath($override).TrimEnd('\', '/')
    if ($root -eq [IO.Path]::GetPathRoot($root).TrimEnd('\', '/')) {
        throw "$($script:OutputRootVariable) must not be a filesystem root: $override"
    }
    $comparison = [StringComparison]::OrdinalIgnoreCase
    if ($repository.Equals($root, $comparison) -or $repository.StartsWith($root + [IO.Path]::DirectorySeparatorChar, $comparison)) {
        throw "$($script:OutputRootVariable) must not be or contain the repository: $override"
    }
    return $root
}

# Scratch space: under the overridden root when configured, otherwise <repository>/tmp.
function Get-ScratchRoot {
    param([Parameter(Mandatory)][string]$RepositoryRoot)

    if ($null -eq (Get-OutputRootOverride)) { return (Join-Path ([IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\', '/')) 'tmp') }
    return (Join-Path (Get-OutputRoot -RepositoryRoot $RepositoryRoot) 'tmp')
}

function Test-OutputRootExternal {
    param([Parameter(Mandatory)][string]$RepositoryRoot)
    $root = Get-OutputRoot -RepositoryRoot $RepositoryRoot
    $repository = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\', '/')
    return -not $root.StartsWith($repository + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
}

# Creates the root and, for an external root, the ownership marker that authorizes cleanup of its contents.
function Initialize-OutputRoot {
    param([Parameter(Mandatory)][string]$RepositoryRoot)

    $root = Get-OutputRoot -RepositoryRoot $RepositoryRoot
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    if (Test-OutputRootExternal -RepositoryRoot $RepositoryRoot) {
        $marker = Join-Path $root $script:OutputRootMarker
        if (-not (Test-Path -LiteralPath $marker)) {
            if (@(Get-ChildItem -LiteralPath $root -Force).Count -gt 0) {
                throw "External output root is not empty and has no ownership marker; refusing to adopt it: $root"
            }
            Set-Content -LiteralPath $marker -Value 'Managed by OnlyWinget scripts; contents may be removed by check/clean.' -Encoding utf8
        }
    }
    return $root
}

# Removes the contents of an external output root, only when this tooling created it (marker present).
function Clear-ExternalOutputRoot {
    param([Parameter(Mandatory)][string]$RepositoryRoot, [switch]$DryRun)

    if (-not (Test-OutputRootExternal -RepositoryRoot $RepositoryRoot)) { return }
    $root = Get-OutputRoot -RepositoryRoot $RepositoryRoot
    if (-not (Test-Path -LiteralPath $root)) { return }
    if (-not (Test-Path -LiteralPath (Join-Path $root $script:OutputRootMarker))) {
        throw "External output root has no ownership marker; refusing to clean it: $root"
    }
    foreach ($child in Get-ChildItem -LiteralPath $root -Force | Where-Object Name -ne $script:OutputRootMarker) {
        if ($DryRun) { Write-Host "[dry-run] remove $($child.FullName)" -ForegroundColor Yellow }
        else { Remove-Item -LiteralPath $child.FullName -Recurse -Force -ErrorAction Stop }
    }
}
