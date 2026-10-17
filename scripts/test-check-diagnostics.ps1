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
    Write-Host 'PASS: Check retains stdout/stderr and failure reports in default/Fast/Full modes, stops on restore failure and reports later failures.'
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
