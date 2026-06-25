param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$RunWingetSmoke
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/ScriptHelpers.ps1')

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
$targetFramework = 'net10.0-windows10.0.17763.0'
$steps = [System.Collections.Generic.List[string]]::new()
$smokeTestStatus = 'not_run'

function Invoke-Step {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Action
    $steps.Add("${Name}: OK")
}

function Assert-LastExitCode {
    param(
        [string]$FailureMessage
    )

    if ($LASTEXITCODE -ne 0) {
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
}

Invoke-Step 'restore' {
    dotnet restore $solutionPath --locked-mode
    Assert-LastExitCode 'dotnet restore fallito.'
}

Invoke-Step 'format' {
    & $formatScriptPath -NoRestore
}

Invoke-Step 'script lint' {
    & $scriptLintPath
}

Invoke-Step 'typecheck' {
    & $typecheckScriptPath -Configuration $Configuration -NoRestore
}

Invoke-Step 'unit test' {
    New-Item -ItemType Directory -Path $testResultsPath -Force | Out-Null
    dotnet test $testProjectPath -c $Configuration --no-restore --results-directory $testResultsPath --logger "trx;LogFileName=unit-tests.trx"
    Assert-LastExitCode 'dotnet test fallito.'
}

Invoke-Step 'integration/e2e test' {
    if (-not $RunWingetSmoke) {
        $script:smokeTestStatus = 'not_run'
        Write-Host 'Smoke test winget reali: not_run. Usa -RunWingetSmoke per abilitarli.' -ForegroundColor DarkGray
        return
    }

    $env:ONLYWINGET_RUN_WINGET_SMOKE = '1'
    try {
        dotnet test $testProjectPath -c $Configuration --no-build --no-restore --filter "Category=Smoke" --results-directory $testResultsPath --logger "trx;LogFileName=winget-smoke-tests.trx"
        Assert-LastExitCode 'dotnet test smoke fallito.'
        $script:smokeTestStatus = 'passed'
    }
    finally {
        Remove-Item Env:\ONLYWINGET_RUN_WINGET_SMOKE -ErrorAction SilentlyContinue
    }
}

Invoke-Step 'build' {
    & $buildScriptPath -Configuration $Configuration -NoRestore
}

Invoke-Step 'setup package' {
    & $packageScriptPath -Configuration $Configuration -NoRestore
}

Invoke-Step 'artifact analysis' {
    New-Item -ItemType Directory -Path $artifactsPath -Force | Out-Null
    $artifact = Get-Item (Join-Path $repoRoot "artifacts/bin/OnlyWinget/$Configuration/$targetFramework/OnlyWinget.exe")
    $distPath = Join-Path $repoRoot "artifacts/dist/OnlyWinget/$Configuration"
    $setup = Get-ChildItem -Path $distPath -Filter '*-setup.exe' |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    $msis = @(Get-ChildItem -Path (Join-Path $distPath 'msi') -Filter '*.msi' |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 2)

    if ($null -eq $setup) {
        throw 'Setup unificato non trovato dopo il packaging.'
    }

    if ($msis.Count -lt 2) {
        throw 'MSI interni x86/x64 non trovati dopo il packaging.'
    }

    @(
        "Configuration: $Configuration"
        "AppArtifact: $($artifact.FullName)"
        "AppSizeBytes: $($artifact.Length)"
        "UnifiedSetupArtifact: $($setup.FullName)"
        "UnifiedSetupSizeBytes: $($setup.Length)"
        "InternalMsiArtifacts: $($msis.FullName -join '; ')"
        "InternalMsiSizeBytes: $(($msis | ForEach-Object { $_.Length }) -join '; ')"
        "SmokeTests: $smokeTestStatus"
        "GeneratedAt: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    ) | Set-Content -Path $reportPath -Encoding UTF8
}

Write-Host ''
Write-Host 'Riepilogo finale:' -ForegroundColor Green
foreach ($step in $steps) {
    Write-Host " - $step"
}
Write-Host "Report artefatti: $reportPath"
