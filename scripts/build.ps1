param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoRestore,
    [switch]$StopRunningInstance,
    [switch]$WarnAsError
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/ScriptHelpers.ps1')

$repoRoot = Split-Path $PSScriptRoot -Parent
$solutionPath = Join-Path $repoRoot 'OnlyWinget.sln'
$projectPath = Join-Path $repoRoot 'src/OnlyWinget/OnlyWinget.csproj'
$targetFramework = 'net8.0-windows'
$outputExePath = Join-Path $repoRoot "artifacts/bin/OnlyWinget/$Configuration/$targetFramework/OnlyWinget.exe"

Assert-Command -Name 'dotnet'

Assert-Path -Path $solutionPath -Description 'Solution'
Assert-Path -Path $projectPath -Description 'Project file'

Assert-ExecutableNotLocked -KillProcess:$StopRunningInstance -ActionName 'Build'

if (-not $NoRestore) {
    dotnet restore $solutionPath --locked-mode
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet restore fallito.'
    }
}

$buildArgs = @($projectPath, '-c', $Configuration)
if ($NoRestore) {
    $buildArgs += '--no-restore'
}
if ($WarnAsError) {
    $buildArgs += '-warnaserror'
}

dotnet build @buildArgs
    if ($LASTEXITCODE -ne 0) {
        if (Test-Path $outputExePath) {
            Assert-ExecutableNotLocked -KillProcess:$StopRunningInstance -ActionName 'Build'
        }

        throw 'dotnet build fallito.'
    }

Write-Host "Build completata ($Configuration)." -ForegroundColor Green
