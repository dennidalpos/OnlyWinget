param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoRestore,
    [switch]$NoBuild,
    [switch]$RunWingetSmoke,
    [switch]$NonInteractive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/ScriptHelpers.ps1')

if (Enter-InteractiveModeIfNoParameter -BoundParameters $PSBoundParameters -ScriptRoot $PSScriptRoot -NonInteractive:$NonInteractive) {
    return
}

$repoRoot = Split-Path $PSScriptRoot -Parent
$testProjectPath = Join-Path $repoRoot 'tests/OnlyWinget.Tests/OnlyWinget.Tests.csproj'
$testResultsPath = Join-Path $repoRoot 'artifacts/test-results'

Assert-Command -Name 'dotnet'
Assert-Path -Path $testProjectPath -Description 'Test project'

New-Item -ItemType Directory -Path $testResultsPath -Force | Out-Null

$testArgs = @($testProjectPath, '-c', $Configuration, '--results-directory', $testResultsPath, '--logger', 'trx;LogFileName=unit-tests.trx')
if (-not $NoRestore) {
    dotnet restore $testProjectPath -r win-x64 --locked-mode
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet restore per i test fallito.'
    }
}

$testArgs += '--no-restore'
if ($NoBuild) {
    $testArgs += '--no-build'
}

dotnet test @testArgs
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet test fallito.'
}

if (-not $RunWingetSmoke) {
    Write-Host 'Smoke test winget reali: not_run. Usa -RunWingetSmoke per abilitarli.' -ForegroundColor DarkGray
    return
}

$env:ONLYWINGET_RUN_WINGET_SMOKE = '1'
try {
    dotnet test $testProjectPath -c $Configuration --no-build --no-restore --filter 'Category=Smoke' --results-directory $testResultsPath --logger 'trx;LogFileName=winget-smoke-tests.trx'
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet test smoke fallito.'
    }
}
finally {
    Remove-Item Env:\ONLYWINGET_RUN_WINGET_SMOKE -ErrorAction SilentlyContinue
}
