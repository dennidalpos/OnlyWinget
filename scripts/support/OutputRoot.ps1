# Single in-repository root for managed build, package, test, fixture and scratch output.
# Everything lives under <repository>/artifacts, which is Git-ignored and cleaned by check/clean.
function Get-OutputRoot {
    param([Parameter(Mandatory)][string]$RepositoryRoot)
    return (Join-Path ([IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\', '/')) 'artifacts')
}

# Scratch space for isolated fixtures and temporary files.
function Get-ScratchRoot {
    param([Parameter(Mandatory)][string]$RepositoryRoot)
    return (Join-Path (Get-OutputRoot -RepositoryRoot $RepositoryRoot) 'tmp')
}

function Initialize-OutputRoot {
    param([Parameter(Mandatory)][string]$RepositoryRoot)
    $root = Get-OutputRoot -RepositoryRoot $RepositoryRoot
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    return $root
}
