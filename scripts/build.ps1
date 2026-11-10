param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoRestore,
    [switch]$StopRunningInstance,
    [switch]$WarnAsError,
    [switch]$NonInteractive,
    [switch]$Fast,
    [switch]$Full
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/ScriptHelpers.ps1')

$isFastMode = -not $Full

$repoRoot = Split-Path $PSScriptRoot -Parent
$solutionPath = Join-Path $repoRoot 'OnlyWinget.sln'
$projectPath = Join-Path $repoRoot 'src/OnlyWinget/OnlyWinget.csproj'
$targetFramework = 'net10.0-windows10.0.17763.0'
$outputExePath = Join-Path $repoRoot "artifacts/bin/OnlyWinget/$Configuration/$targetFramework/win-x64/OnlyWinget.exe"

Assert-Command -Name 'dotnet'

Assert-Path -Path $solutionPath -Description 'Solution'
Assert-Path -Path $projectPath -Description 'Project file'

Assert-ExecutableNotLocked -KillProcess:$StopRunningInstance -ActionName 'Build'

if (-not $NoRestore) {
    if ($isFastMode) {
        dotnet restore $solutionPath --locked-mode > $null
    } else {
        dotnet restore $solutionPath --locked-mode
    }
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet restore fallito.'
    }
}

$buildArgs = @($solutionPath, '-c', $Configuration)
if ($NoRestore) {
    $buildArgs += '--no-restore'
}
if ($WarnAsError) {
    $buildArgs += '-warnaserror'
}
if ($isFastMode) {
    $buildArgs += '--verbosity'
    $buildArgs += 'quiet'
    $buildArgs += '-clp:ErrorsOnly'
}

dotnet build @buildArgs
if ($LASTEXITCODE -ne 0) {
    if (Test-Path $outputExePath) {
        Assert-ExecutableNotLocked -KillProcess:$StopRunningInstance -ActionName 'Build'
    }

    throw 'dotnet build fallito.'
}

if ($isFastMode) {
    Write-Host "PASS: Build succeeded ($Configuration)." -ForegroundColor Green
} else {
    Write-Host "Build completata ($Configuration)." -ForegroundColor Green
}

