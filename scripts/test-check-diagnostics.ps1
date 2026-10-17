$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('OnlyWinget-check-tests-' + [Guid]::NewGuid().ToString('N'))
$fixtureScripts = Join-Path $fixtureRoot 'scripts'
$fixtureSupport = Join-Path $fixtureScripts 'support'
$fixtureProject = Join-Path $fixtureRoot 'tests/OnlyWinget.Tests'
$process = $null
New-Item -ItemType Directory -Path $fixtureSupport, $fixtureProject -Force | Out-Null

try {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'check.ps1') -Destination $fixtureScripts
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'support/ScriptHelpers.ps1') -Destination $fixtureSupport
    [IO.File]::WriteAllText((Join-Path $fixtureRoot 'OnlyWinget.sln'), '')
    [IO.File]::WriteAllText((Join-Path $fixtureProject 'OnlyWinget.Tests.csproj'), '<Project />')
    foreach ($script in @('build', 'package', 'lint', 'typecheck', 'test')) {
        [IO.File]::WriteAllText((Join-Path $fixtureScripts "$script.ps1"), "throw 'Unexpected fixture step: $script'")
    }
    @'
[IO.File]::WriteAllText((Join-Path (Split-Path $PSScriptRoot -Parent) 'format-ran.txt'), 'format reached')
throw 'intentional failure after restore'
'@ | Set-Content -LiteralPath (Join-Path $fixtureScripts 'format.ps1') -Encoding utf8

    foreach ($restoreExit in @(7, 0)) {
        [IO.File]::WriteAllText((Join-Path $fixtureRoot 'dotnet.cmd'), "@echo off`r`necho intentional restore stdout`r`necho intentional restore stderr 1>&2`r`nexit /b $restoreExit`r`n")
        foreach ($mode in @('Default', 'Fast', 'Full')) {
            $start = [Diagnostics.ProcessStartInfo]::new((Get-Command pwsh -ErrorAction Stop).Source)
            $start.UseShellExecute = $false
            $start.CreateNoWindow = $true
            $start.WorkingDirectory = $fixtureRoot
            $start.RedirectStandardOutput = $true
            $start.RedirectStandardError = $true
            $start.Environment['PATH'] = $fixtureRoot + ';' + $env:PATH
            foreach ($argument in @('-NoProfile', '-File', (Join-Path $fixtureScripts 'check.ps1'), '-NonInteractive')) {
                $start.ArgumentList.Add($argument)
            }
            if ($mode -ne 'Default') { $start.ArgumentList.Add("-$mode") }
            $process = [Diagnostics.Process]::Start($start)
            $output = $process.StandardOutput.ReadToEndAsync()
            $errors = $process.StandardError.ReadToEndAsync()
            $process.WaitForExit()
            $combinedOutput = $output.Result + $errors.Result
            if ($process.ExitCode -eq 0) { throw "Check masked a fixture failure (restore=$restoreExit, mode=$mode)." }
            $logPath = Join-Path $fixtureRoot 'artifacts/check/restore.log'
            if (-not (Test-Path -LiteralPath $logPath)) { throw "Restore diagnostics were lost (restore=$restoreExit, mode=$mode)." }
            $log = [IO.File]::ReadAllText($logPath)
            if ($log -notmatch 'intentional restore stdout' -or $log -notmatch 'intentional restore stderr') {
                throw 'Restore log lost stdout or stderr.'
            }
            $reportPath = Join-Path $fixtureRoot 'artifacts/build-report.txt'
            if (-not (Test-Path -LiteralPath $reportPath)) { throw 'Failed gate did not retain a report.' }
            $report = [IO.File]::ReadAllText($reportPath)
            $expectedStep = if ($restoreExit -eq 0) { 'format' } else { 'restore' }
            if ($report -notmatch "FailedStep: $expectedStep" -or $report -notmatch 'Result: FAILED') {
                throw "Report did not identify failed step $expectedStep."
            }
            if ($restoreExit -ne 0) {
                if ($combinedOutput -notmatch 'intentional restore stdout' -or $combinedOutput -notmatch 'intentional restore stderr' -or
                    $combinedOutput -notmatch 'exit 7' -or $combinedOutput -notmatch 'Diagnostics:') {
                    throw "Restore failure was not actionable in console output (mode=$mode)."
                }
                if (Test-Path -LiteralPath (Join-Path $fixtureRoot 'format-ran.txt')) { throw 'Check continued after a failed restore.' }
            } elseif ($report -notmatch 'restore: OK' -or $report -notmatch 'intentional failure after restore') {
                throw 'Report lost the completed restore or the later failure.'
            }
            $process.Dispose()
            $process = $null
        }
    }

    # Use a real SDK/NuGet failure too; the command-stub cases above cover both output streams.
    $realRoot = Join-Path $fixtureRoot 'real-restore'
    $realScripts = Join-Path $realRoot 'scripts'
    $realSupport = Join-Path $realScripts 'support'
    $realProjectRoot = Join-Path $realRoot 'tests/OnlyWinget.Tests'
    New-Item -ItemType Directory -Path $realSupport, $realProjectRoot -Force | Out-Null
    foreach ($script in @('check', 'build', 'package', 'lint', 'typecheck', 'test', 'format')) {
        Copy-Item -LiteralPath (Join-Path $fixtureScripts "$script.ps1") -Destination $realScripts
    }
    Copy-Item -LiteralPath (Join-Path $fixtureSupport 'ScriptHelpers.ps1') -Destination $realSupport
    Copy-Item -LiteralPath (Join-Path (Split-Path $PSScriptRoot -Parent) 'global.json') -Destination $realRoot
    $realProject = Join-Path $realProjectRoot 'OnlyWinget.Tests.csproj'
    [IO.File]::WriteAllText($realProject, '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><RestorePackagesWithLockFile>true</RestorePackagesWithLockFile></PropertyGroup></Project>')
    [IO.File]::WriteAllText((Join-Path $realProjectRoot 'packages.lock.json'), '{"version":1,"dependencies":{"net10.0":{},"net10.0/win-x64":{}}}')
    $dotnet = (Get-Command dotnet -ErrorAction Stop).Source
    $generation = & $dotnet new sln --format sln --name OnlyWinget --output $realRoot 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Solution fixture generation failed: $generation" }
    $generation = & $dotnet sln (Join-Path $realRoot 'OnlyWinget.sln') add $realProject 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Solution fixture project addition failed: $generation" }

    $start = [Diagnostics.ProcessStartInfo]::new((Get-Command pwsh -ErrorAction Stop).Source)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WorkingDirectory = $realRoot
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in @('-NoProfile', '-File', (Join-Path $realScripts 'check.ps1'), '-NonInteractive')) {
        $start.ArgumentList.Add($argument)
    }
    $process = [Diagnostics.Process]::Start($start)
    $output = $process.StandardOutput.ReadToEndAsync()
    $errors = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    $combinedOutput = $output.Result + $errors.Result
    $realLog = Join-Path $realRoot 'artifacts/check/restore.log'
    $realReport = Join-Path $realRoot 'artifacts/build-report.txt'
    if ($process.ExitCode -eq 0 -or $combinedOutput -notmatch 'NU1004' -or $combinedOutput -notmatch 'Diagnostics:' -or
        -not (Test-Path -LiteralPath $realLog) -or -not (Test-Path -LiteralPath $realReport)) {
        throw "Real locked restore failure was not retained: $combinedOutput"
    }
    if ([IO.File]::ReadAllText($realLog) -notmatch 'NU1004' -or [IO.File]::ReadAllText($realReport) -notmatch 'FailedStep: restore') {
        throw 'Real NuGet failure did not identify the restore step and error code.'
    }
    $retainedRoot = Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/check'
    New-Item -ItemType Directory -Path $retainedRoot -Force | Out-Null
    Copy-Item -LiteralPath $realLog -Destination (Join-Path $retainedRoot 'restore-failure-fixture.log')
    Copy-Item -LiteralPath $realReport -Destination (Join-Path $retainedRoot 'restore-failure-fixture-report.log')
    [IO.File]::WriteAllText((Join-Path $retainedRoot 'restore-failure-fixture-console.log'), $combinedOutput)
    $process.Dispose()
    $process = $null
    Write-Host 'PASS: Check retains default/Fast/Full failure diagnostics and rejects a real NU1004 restore; fixture logs/report are retained for CI upload.'
}
finally {
    if ($null -ne $process) {
        if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
        $process.Dispose()
    }
    $resolvedRoot = [IO.Path]::GetFullPath($fixtureRoot)
    $expectedParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (-not $resolvedRoot.StartsWith($expectedParent + 'OnlyWinget-check-tests-', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Unexpected check fixture cleanup path.'
    }
    Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
}
