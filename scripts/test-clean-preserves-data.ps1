$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/ScriptHelpers.ps1')

$repoRoot = Split-Path $PSScriptRoot -Parent
$fixtureRoot = Join-Path $repoRoot "tmp/clean-regression-$([Guid]::NewGuid().ToString('N'))"
$fixtureRepo = Join-Path $fixtureRoot 'repository'
$fixtureData = Join-Path $fixtureRoot 'local-data'
$previousLocalAppData = $env:LOCALAPPDATA

try {
    New-Item -ItemType Directory -Path "$fixtureRepo/scripts/support", "$fixtureRepo/src", "$fixtureRepo/artifacts", "$fixtureData/OnlyWinget/logs" -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'clean.ps1') -Destination "$fixtureRepo/scripts/clean.ps1"
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'support/ScriptHelpers.ps1') -Destination "$fixtureRepo/scripts/support/ScriptHelpers.ps1"
    Set-Content -LiteralPath "$fixtureRepo/src/Fixture.csproj" -Value '<Project />' -Encoding utf8
    Set-Content -LiteralPath "$fixtureRepo/artifacts/generated.txt" -Value 'generated' -Encoding utf8
    $sentinels = @('onlywinget.db', 'workspace-v1.json', 'settings.json', 'source-preferences-v1.json', 'logs/sentinel.log')
    foreach ($relativePath in $sentinels) {
        Set-Content -LiteralPath "$fixtureData/OnlyWinget/$relativePath" -Value 'preserve me' -Encoding utf8
    }

    $env:LOCALAPPDATA = $fixtureData
    & "$fixtureRepo/scripts/clean.ps1" -All -DryRun -NonInteractive
    if (-not (Test-Path -LiteralPath "$fixtureRepo/artifacts/generated.txt")) {
        throw 'Dry run removed a generated fixture.'
    }

    # Stub only dotnet in this fixture; never clear the machine's NuGet caches.
    & {
        function dotnet { $global:LASTEXITCODE = 0 }
        & "$fixtureRepo/scripts/clean.ps1" -All -NonInteractive
    }

    if (Test-Path -LiteralPath "$fixtureRepo/artifacts") {
        throw 'Clean did not remove the generated fixture.'
    }
    foreach ($relativePath in $sentinels) {
        $sentinelPath = "$fixtureData/OnlyWinget/$relativePath"
        if (-not (Test-Path -LiteralPath $sentinelPath) -or
            (Get-Content -LiteralPath $sentinelPath -Raw).Trim() -ne 'preserve me') {
            throw "Clean modified application data: $relativePath"
        }
    }
    Write-Host 'PASS: Clean -All preserves workspace, settings, source preferences and logs.' -ForegroundColor Green
}
finally {
    $env:LOCALAPPDATA = $previousLocalAppData
    $cleanupPath = Assert-RepositoryPathInAllowedRoot -Path $fixtureRoot -RepositoryRoot $repoRoot -AllowedRoots @((Join-Path $repoRoot 'tmp'))
    if (Test-Path -LiteralPath $cleanupPath) {
        Remove-Item -LiteralPath $cleanupPath -Recurse -Force
    }
}
