param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$NoRestore,
    [switch]$NoBuild,
    [switch]$RunWingetSmoke,
    [switch]$NonInteractive,
    [switch]$Fast,
    [switch]$Full
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/ScriptHelpers.ps1')

$repoRoot = Split-Path $PSScriptRoot -Parent
$testProjectPath = Join-Path $repoRoot 'tests/OnlyWinget.Tests/OnlyWinget.Tests.csproj'
$testResultsPath = Join-Path $repoRoot 'artifacts/test-results'
Assert-Command -Name 'dotnet'
Assert-Path -Path $testProjectPath -Description 'Test project'
New-Item -ItemType Directory -Path $testResultsPath -Force | Out-Null

function Invoke-TestSuite {
    param([string[]]$Arguments, [string]$ResultName)
    $trxPath = Join-Path $testResultsPath "$ResultName.trx"
    $logPath = Join-Path $testResultsPath "$ResultName.log"
    if (Test-Path -LiteralPath $trxPath) { Remove-Item -LiteralPath $trxPath -Force }
    if ($Full) {
        & dotnet @Arguments 2>&1 | Tee-Object -FilePath $logPath | Out-Host
    } else {
        & dotnet @Arguments 2>&1 | Out-File -LiteralPath $logPath -Encoding utf8
    }
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0 -or -not (Test-Path -LiteralPath $trxPath)) {
        Get-Content -LiteralPath $logPath -Tail 60 | Out-Host
        throw "dotnet test failed (exit $exitCode). Diagnostics: $logPath"
    }
    [xml]$trx = Get-Content -LiteralPath $trxPath
    $results = @($trx.TestRun.Results.UnitTestResult)
    $passed = @($results | Where-Object outcome -eq 'Passed').Count
    $skipped = @($results | Where-Object outcome -eq 'NotExecuted').Count
    if ($passed -eq 0 -or $results.Count -ne ($passed + $skipped)) {
        Get-Content -LiteralPath $logPath -Tail 60 | Out-Host
        throw "Test suite has failed/unknown outcomes or no executed tests. Diagnostics: $logPath"
    }
    Write-Host "PASS: $passed executed tests passed; $skipped skipped." -ForegroundColor Green
}

if (-not $NoRestore) {
    $restoreLog = Join-Path $testResultsPath 'test-restore.log'
    & dotnet restore $testProjectPath --locked-mode 2>&1 | Tee-Object -FilePath $restoreLog | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Test restore failed. Diagnostics: $restoreLog" }
}

$testArgs = @('test', $testProjectPath, '-c', $Configuration, '--no-restore', '--filter', 'Category!=Smoke', '--results-directory', $testResultsPath, '--logger', 'trx;LogFileName=unit-tests.trx', '--maxcpucount:1')
if ($NoBuild) { $testArgs += '--no-build' }
Invoke-TestSuite -Arguments $testArgs -ResultName 'unit-tests'

if (-not $RunWingetSmoke) {
    Write-Host 'Live smoke tests: not_run (excluded from offline suite).' -ForegroundColor DarkGray
    return
}

$previousSmokeSetting = $env:ONLYWINGET_RUN_WINGET_SMOKE
try {
    $env:ONLYWINGET_RUN_WINGET_SMOKE = '1'
    $smokeArgs = @('test', $testProjectPath, '-c', $Configuration, '--no-build', '--no-restore', '--filter', 'Category=Smoke', '--results-directory', $testResultsPath, '--logger', 'trx;LogFileName=winget-smoke-tests.trx')
    Invoke-TestSuite -Arguments $smokeArgs -ResultName 'winget-smoke-tests'
}
finally { $env:ONLYWINGET_RUN_WINGET_SMOKE = $previousSmokeSetting }
