$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/VerificationHelpers.ps1')
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$fixtureRoot = Join-Path $repositoryRoot ('artifacts/verification-tests/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
$process = $null
try {
    $failureCommand = Join-Path $fixtureRoot 'failure.cmd'
    $sentinel = Join-Path $fixtureRoot 'unexpected-success.txt'
    [IO.File]::WriteAllText($failureCommand, "@echo off`r`necho intentional native failure`r`nexit /b 7`r`n")
    $failed = $false
    try {
        Invoke-CheckedNativeCommand -Command $failureCommand -Arguments @() | Out-Null
        [IO.File]::WriteAllText($sentinel, 'earlier failure masked')
    } catch {
        if ($_.Exception.Message -notmatch 'exit code 7') { throw }
        $failed = $true
    }
    if (-not $failed -or (Test-Path -LiteralPath $sentinel)) { throw 'Early native failure was masked.' }

    $sourcePath = Join-Path $fixtureRoot 'Window.cs'
    $exePath = Join-Path $fixtureRoot 'Fixture.exe'
    @'
using System;
using System.Threading;
using System.Windows.Forms;
class Program {
    [STAThread] static void Main(string[] args) {
        if (args[0] == "early-zero") return;
        if (args[0] == "headless") { Thread.Sleep(10000); return; }
        Application.Run(new Form { Text = "Startup fixture", WindowState = FormWindowState.Minimized });
    }
}
'@ | Set-Content -LiteralPath $sourcePath -Encoding utf8
    & (Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe') /nologo /target:winexe /reference:System.Windows.Forms.dll "/out:$exePath" $sourcePath
    if ($LASTEXITCODE -ne 0) { throw 'Startup fixture compilation failed.' }
    foreach ($mode in @('early-zero', 'headless', 'window')) {
        $process = Start-Process -FilePath $exePath -ArgumentList $mode -WindowStyle Hidden -PassThru
        try {
            $failed = $false
            try { Assert-ResponsiveStartup -Process $process -WaitSeconds 1 } catch {
                if ($_.Exception.Message -notmatch 'exited during startup|responsive main window') { throw }
                $failed = $true
            }
            if ($failed -ne ($mode -ne 'window')) { throw "Incorrect startup outcome: $mode" }
        } finally {
            if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
            $process.Dispose(); $process = $null
        }
    }

    # Run the real test script with a fixture-only dotnet command that fails before TRX creation.
    $fixtureScripts = Join-Path $fixtureRoot 'scripts'
    $fixtureSupport = Join-Path $fixtureScripts 'support'
    $fixtureProject = Join-Path $fixtureRoot 'tests/OnlyWinget.Tests'
    New-Item -ItemType Directory -Path $fixtureSupport, $fixtureProject -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'test.ps1') -Destination $fixtureScripts
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'support/ScriptHelpers.ps1') -Destination $fixtureSupport
    [IO.File]::WriteAllText((Join-Path $fixtureProject 'OnlyWinget.Tests.csproj'), '<Project />')
    Copy-Item -LiteralPath $failureCommand -Destination (Join-Path $fixtureRoot 'dotnet.cmd')
    foreach ($full in @($false, $true)) {
        $start = [Diagnostics.ProcessStartInfo]::new((Get-Command pwsh -ErrorAction Stop).Source)
        $start.UseShellExecute = $false
        $start.CreateNoWindow = $true
        $start.RedirectStandardOutput = $true
        $start.RedirectStandardError = $true
        $start.Environment['PATH'] = $fixtureRoot + ';' + $env:PATH
        foreach ($argument in @('-NoProfile', '-File', (Join-Path $fixtureScripts 'test.ps1'), '-NoRestore', '-NoBuild')) { $start.ArgumentList.Add($argument) }
        if ($full) { $start.ArgumentList.Add('-Full') }
        $process = [Diagnostics.Process]::Start($start)
        $output = $process.StandardOutput.ReadToEndAsync()
        $errors = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        if ($process.ExitCode -eq 0 -or $output.Result -notmatch 'intentional native failure' -or $errors.Result -notmatch 'Diagnostics:') {
            throw "Test runner masked pre-TRX failure (Full=$full)."
        }
        $log = Join-Path $fixtureRoot 'artifacts/test-results/unit-tests.log'
        if ([IO.File]::ReadAllText($log) -notmatch 'intentional native failure') { throw 'Native failure log was not retained.' }
        $process.Dispose(); $process = $null
    }
    Write-Host 'PASS: early native/pre-TRX failures retain diagnostics; zero exit/headless startup fail; responsive window passes.'
}
finally {
    if ($null -ne $process) {
        if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
        $process.Dispose()
    }
    $resolvedRoot = [IO.Path]::GetFullPath($fixtureRoot)
    if (-not $resolvedRoot.StartsWith((Join-Path $repositoryRoot 'artifacts/verification-tests/'), [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Unexpected verification fixture cleanup path.'
    }
    Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
}
