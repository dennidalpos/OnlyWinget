param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$RunWingetSmoke,
    [switch]$StopRunningInstance,
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
$testProjectPath = Join-Path $repoRoot 'tests/OnlyWinget.Tests/OnlyWinget.Tests.csproj'
$artifactsPath = Join-Path $repoRoot 'artifacts'
$tmpPath = Join-Path $repoRoot 'tmp'
$reportPath = Join-Path $artifactsPath 'build-report.txt'
$testResultsPath = Join-Path $artifactsPath 'test-results'
$scriptsRoot = $PSScriptRoot
$buildScriptPath = Join-Path $scriptsRoot 'build.ps1'
$formatScriptPath = Join-Path $scriptsRoot 'format.ps1'
$packageScriptPath = Join-Path $scriptsRoot 'package.ps1'
$scriptLintPath = Join-Path $scriptsRoot 'lint.ps1'
$typecheckScriptPath = Join-Path $scriptsRoot 'typecheck.ps1'
$testScriptPath = Join-Path $scriptsRoot 'test.ps1'
$targetFramework = 'net10.0-windows10.0.17763.0'
$steps = [System.Collections.Generic.List[string]]::new()
$smokeTestStatus = 'not_run'

function Invoke-Step {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    if (-not $isFastMode) {
        Write-Host "==> $Name" -ForegroundColor Cyan
    }
    & $Action
    Assert-LastExitCode "$Name fallito."
    $steps.Add("${Name}: OK")
}

function Assert-LastExitCode {
    param(
        [string]$FailureMessage
    )

    if ((Test-Path Variable:\LASTEXITCODE) -and $LASTEXITCODE -ne 0) {
        throw $FailureMessage
    }
}

function Remove-CheckGeneratedPath {
    param(
        [string]$Path
    )

    $fullPath = Assert-RepositoryPathInAllowedRoot `
        -Path $Path `
        -RepositoryRoot $repoRoot `
        -AllowedRoots @($artifactsPath, $tmpPath) `
        -Description 'Pulizia check'

    if (-not (Test-Path -LiteralPath $fullPath)) {
        return
    }

    Remove-Item -LiteralPath $fullPath -Recurse -Force -ErrorAction Stop
}

Assert-Command -Name 'dotnet'
Assert-Path -Path $solutionPath -Description 'Solution'
Assert-Path -Path $testProjectPath -Description 'Test project'
Assert-Path -Path $buildScriptPath -Description 'Build script'
Assert-Path -Path $formatScriptPath -Description 'Format script'
Assert-Path -Path $packageScriptPath -Description 'Packaging script'
Assert-Path -Path $scriptLintPath -Description 'PowerShell script lint script'
Assert-Path -Path $typecheckScriptPath -Description 'Typecheck script'

Invoke-Step 'clean generated outputs' {
    Remove-CheckGeneratedPath -Path $artifactsPath
    Remove-CheckGeneratedPath -Path $tmpPath
    if ($isFastMode) { Write-Host 'PASS: Cleaned outputs.' -ForegroundColor Green }
}

Invoke-Step 'restore' {
    if ($isFastMode) {
        dotnet restore $solutionPath --locked-mode > $null
    } else {
        dotnet restore $solutionPath --locked-mode
    }
    Assert-LastExitCode 'dotnet restore fallito.'
    if ($isFastMode) { Write-Host 'PASS: Restore completed.' -ForegroundColor Green }
}

Invoke-Step 'format' {
    & $formatScriptPath -NoRestore -Full:$Full -NonInteractive:$NonInteractive
}

Invoke-Step 'script lint' {
    & $scriptLintPath -Full:$Full -NonInteractive:$NonInteractive
}

Invoke-Step 'typecheck' {
    & $typecheckScriptPath -Configuration $Configuration -NoRestore -StopRunningInstance:$StopRunningInstance -Full:$Full -NonInteractive:$NonInteractive
}

Invoke-Step 'unit test' {
    & $testScriptPath -Configuration $Configuration -NoRestore -Full:$Full -NonInteractive:$NonInteractive
}

Invoke-Step 'integration/e2e test' {
    if (-not $RunWingetSmoke) {
        $script:smokeTestStatus = 'not_run'
        if (-not $isFastMode) {
            Write-Host 'Smoke test winget reali: not_run. Usa -RunWingetSmoke per abilitarli.' -ForegroundColor DarkGray
        }
        return
    }

    $env:ONLYWINGET_RUN_WINGET_SMOKE = '1'
    try {
        if ($isFastMode) {
            dotnet test $testProjectPath -c $Configuration --no-build --no-restore --filter "Category=Smoke" --results-directory $testResultsPath --logger "trx;LogFileName=winget-smoke-tests.trx" --logger "console;verbosity=quiet" --verbosity quiet > $null
        } else {
            dotnet test $testProjectPath -c $Configuration --no-build --no-restore --filter "Category=Smoke" --results-directory $testResultsPath --logger "trx;LogFileName=winget-smoke-tests.trx"
        }
        Assert-LastExitCode 'dotnet test smoke fallito.'
        $script:smokeTestStatus = 'passed'
        if ($isFastMode) { Write-Host 'PASS: Winget smoke tests passed.' -ForegroundColor Green }
    }
    finally {
        Remove-Item Env:\ONLYWINGET_RUN_WINGET_SMOKE -ErrorAction SilentlyContinue
    }
}

Invoke-Step 'build' {
    & $buildScriptPath -Configuration $Configuration -NoRestore -StopRunningInstance:$StopRunningInstance -Full:$Full -NonInteractive:$NonInteractive
}

Invoke-Step 'setup package' {
    & $packageScriptPath -Configuration $Configuration -NoRestore -StopRunningInstance:$StopRunningInstance -Full:$Full -NonInteractive:$NonInteractive
}

Invoke-Step 'artifact analysis' {
    New-Item -ItemType Directory -Path $artifactsPath -Force | Out-Null
    $artifact = Get-Item (Join-Path $repoRoot "artifacts/bin/OnlyWinget/$Configuration/$targetFramework/win-x64/OnlyWinget.exe")
    $distPath = Join-Path $repoRoot "artifacts/dist/OnlyWinget/$Configuration"
    $setup = Get-ChildItem -Path $distPath -Filter '*-setup.exe' |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    $portable = Get-ChildItem -Path $distPath -Filter '*-portable-x64.zip' |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if ($null -eq $setup) {
        throw 'Setup NSIS non trovato dopo il packaging.'
    }

    if ($null -eq $portable) {
        throw 'Archivio portable x64 non trovato dopo il packaging.'
    }

    @(
        "Configuration: $Configuration"
        "AppArtifact: $($artifact.FullName)"
        "AppSizeBytes: $($artifact.Length)"
        "UnifiedSetupArtifact: $($setup.FullName)"
        "UnifiedSetupSizeBytes: $($setup.Length)"
        "PortableArtifact: $($portable.FullName)"
        "PortableSizeBytes: $($portable.Length)"
        "SmokeTests: $smokeTestStatus"
        "GeneratedAt: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    ) | Set-Content -Path $reportPath -Encoding UTF8
    if ($isFastMode) { Write-Host 'PASS: Artifact analysis completed.' -ForegroundColor Green }
}

Write-Host ''
if ($isFastMode) {
    Write-Host "PASS: Full check gate passed ($($steps.Count) steps OK)." -ForegroundColor Green
} else {
    Write-Host 'Riepilogo finale:' -ForegroundColor Green
    foreach ($step in $steps) {
        Write-Host " - $step"
    }
    Write-Host "Report artefatti: $reportPath"
}

