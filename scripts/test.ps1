param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
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

$isFastMode = -not $Full

$testArgs = @('test', $testProjectPath, '-c', $Configuration, '--results-directory', $testResultsPath, '--logger', 'trx;LogFileName=unit-tests.trx', '--maxcpucount:1')
if (-not $NoRestore) {
    dotnet restore $testProjectPath --locked-mode > $null
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet restore per i test fallito.'
    }
}

$testArgs += '--no-restore'
if ($NoBuild) {
    $testArgs += '--no-build'
}

if ($isFastMode) {
    $testArgs += '--logger'
    $testArgs += 'console;verbosity=quiet'
    $testArgs += '--verbosity'
    $testArgs += 'quiet'
}

$trxFile = Join-Path $testResultsPath 'unit-tests.trx'
if (Test-Path $trxFile) {
    Remove-Item $trxFile -Force -ErrorAction SilentlyContinue
}

$exitCode = 0
try {
    & dotnet @testArgs > $null 2>&1
    $exitCode = $LASTEXITCODE
}
catch {
    $exitCode = 1
}

if ($isFastMode) {
    if ($exitCode -eq 0 -and (Test-Path $trxFile)) {
        [xml]$xml = Get-Content -LiteralPath $trxFile
        $results = @($xml.TestRun.Results.UnitTestResult)
        $passed = @($results | Where-Object { $_.outcome -eq 'Passed' }).Count
        $total = $results.Count
        Write-Host "PASS: $passed/$total unit tests passed." -ForegroundColor Green
    }
    else {
        Write-Host "FAIL: Unit test suite execution failed." -ForegroundColor Red
        if (Test-Path $trxFile) {
            [xml]$xml = Get-Content -LiteralPath $trxFile
            $results = @($xml.TestRun.Results.UnitTestResult)
            $failedTests = @($results | Where-Object { $_.outcome -eq 'Failed' })
            foreach ($failed in $failedTests) {
                Write-Host "FAIL: $($failed.testName)" -ForegroundColor Red
                if ($failed.Output -and $failed.Output.ErrorInfo) {
                    $msg = [string]$failed.Output.ErrorInfo.Message
                    if (-not [string]::IsNullOrWhiteSpace($msg)) {
                        Write-Host "  Message: $($msg.Trim())" -ForegroundColor Red
                    }
                    $stack = [string]$failed.Output.ErrorInfo.StackTrace
                    if (-not [string]::IsNullOrWhiteSpace($stack)) {
                        $shortStack = ($stack.Trim() -split "`r?`n" | Select-Object -First 5) -join "`n"
                        Write-Host "  Stack Trace:`n$shortStack" -ForegroundColor DarkRed
                    }
                }
            }
        }
        throw 'dotnet test fallito.'
    }
}
else {
    if ($exitCode -ne 0) {
        throw 'dotnet test fallito.'
    }
}

if (-not $RunWingetSmoke) {
    if (-not $isFastMode) {
        Write-Host 'Smoke test winget reali: not_run. Usa -RunWingetSmoke per abilitarli.' -ForegroundColor DarkGray
    }
    return
}

$env:ONLYWINGET_RUN_WINGET_SMOKE = '1'
$smokeTrx = Join-Path $testResultsPath 'winget-smoke-tests.trx'
if (Test-Path $smokeTrx) {
    Remove-Item $smokeTrx -Force -ErrorAction SilentlyContinue
}

try {
    $smokeArgs = @('test', $testProjectPath, '-c', $Configuration, '--no-build', '--no-restore', '--filter', 'Category=Smoke', '--results-directory', $testResultsPath, '--logger', 'trx;LogFileName=winget-smoke-tests.trx')
    if ($isFastMode) {
        $smokeArgs += '--logger'
        $smokeArgs += 'console;verbosity=quiet'
        $smokeArgs += '--verbosity'
        $smokeArgs += 'quiet'
    }

    $smokeExit = 0
    try {
        & dotnet @smokeArgs > $null 2>&1
        $smokeExit = $LASTEXITCODE
    }
    catch {
        $smokeExit = 1
    }

    if ($smokeExit -ne 0) {
        if ($isFastMode -and (Test-Path $smokeTrx)) {
            [xml]$xml = Get-Content -LiteralPath $smokeTrx
            $results = @($xml.TestRun.Results.UnitTestResult)
            $failedTests = @($results | Where-Object { $_.outcome -eq 'Failed' })
            foreach ($failed in $failedTests) {
                Write-Host "FAIL: $($failed.testName)" -ForegroundColor Red
                if ($failed.Output -and $failed.Output.ErrorInfo) {
                    $msg = [string]$failed.Output.ErrorInfo.Message
                    if (-not [string]::IsNullOrWhiteSpace($msg)) {
                        Write-Host "  Message: $($msg.Trim())" -ForegroundColor Red
                    }
                    $stack = [string]$failed.Output.ErrorInfo.StackTrace
                    if (-not [string]::IsNullOrWhiteSpace($stack)) {
                        $shortStack = ($stack.Trim() -split "`r?`n" | Select-Object -First 5) -join "`n"
                        Write-Host "  Stack Trace:`n$shortStack" -ForegroundColor DarkRed
                    }
                }
            }
        }
        throw 'dotnet test smoke fallito.'
    }
    if ($isFastMode) {
        Write-Host "PASS: Winget smoke tests passed." -ForegroundColor Green
    }
}
finally {
    Remove-Item Env:\ONLYWINGET_RUN_WINGET_SMOKE -ErrorAction SilentlyContinue
}
