param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$RunWingetSmoke
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'ScriptHelpers.ps1')

$repoRoot = Split-Path $PSScriptRoot -Parent
$solutionPath = Join-Path $repoRoot 'OnlyWinget.sln'
$testProjectPath = Join-Path $repoRoot 'tests/OnlyWinget.Tests/OnlyWinget.Tests.csproj'
$artifactsPath = Join-Path $repoRoot 'artifacts'
$reportPath = Join-Path $artifactsPath 'build-report.txt'
$testResultsPath = Join-Path $artifactsPath 'test-results'
$buildScriptPath = Join-Path $PSScriptRoot 'build.ps1'
$buildMsiScriptPath = Join-Path $PSScriptRoot 'build-msi.ps1'
$targetFramework = 'net8.0-windows'
$steps = [System.Collections.Generic.List[string]]::new()

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

Assert-Command -Name 'dotnet'
Assert-Path -Path $solutionPath -Description 'Solution'
Assert-Path -Path $testProjectPath -Description 'Test project'
Assert-Path -Path $buildScriptPath -Description 'Build script'
Assert-Path -Path $buildMsiScriptPath -Description 'Packaging build script'

Invoke-Step 'restore' {
    dotnet restore $solutionPath --locked-mode
    Assert-LastExitCode 'dotnet restore fallito.'
}

Invoke-Step 'format' {
    dotnet format $solutionPath --verify-no-changes --no-restore
    Assert-LastExitCode 'dotnet format fallito.'
}

Invoke-Step 'lint' {
    & $buildScriptPath -Configuration $Configuration -WarnAsError -NoRestore
}

Invoke-Step 'typecheck' {
    Write-Host 'Typecheck coperto dalla compilazione C#.' -ForegroundColor DarkGray
}

Invoke-Step 'unit test' {
    New-Item -ItemType Directory -Path $testResultsPath -Force | Out-Null
    dotnet test $testProjectPath -c $Configuration --no-restore --results-directory $testResultsPath --logger "trx;LogFileName=unit-tests.trx"
    Assert-LastExitCode 'dotnet test fallito.'
}

Invoke-Step 'integration/e2e test' {
    if (-not $RunWingetSmoke) {
        Write-Host 'Smoke test winget reali disabilitati. Usa -RunWingetSmoke per abilitarli.' -ForegroundColor DarkGray
        return
    }

    $env:ONLYWINGET_RUN_WINGET_SMOKE = '1'
    try {
        dotnet test $testProjectPath -c $Configuration --no-build --no-restore --filter "Category=Smoke" --results-directory $testResultsPath --logger "trx;LogFileName=winget-smoke-tests.trx"
        Assert-LastExitCode 'dotnet test smoke fallito.'
    }
    finally {
        Remove-Item Env:\ONLYWINGET_RUN_WINGET_SMOKE -ErrorAction SilentlyContinue
    }
}

Invoke-Step 'build' {
    & $buildScriptPath -Configuration $Configuration -NoRestore
}

Invoke-Step 'setup package' {
    & $buildMsiScriptPath -Configuration $Configuration -NoRestore
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
        "GeneratedAt: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    ) | Set-Content -Path $reportPath -Encoding UTF8
}

Write-Host ''
Write-Host 'Riepilogo finale:' -ForegroundColor Green
foreach ($step in $steps) {
    Write-Host " - $step"
}
Write-Host "Report artefatti: $reportPath"
