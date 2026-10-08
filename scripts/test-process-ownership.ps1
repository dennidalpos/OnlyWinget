$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/ScriptHelpers.ps1')

$repositoryRoot = $script:OnlyWingetRepositoryRoot
$fixtureRoot = Join-Path $repositoryRoot ('artifacts/process-tests/' + [Guid]::NewGuid().ToString('N'))
$ownedRoot = Join-Path $fixtureRoot 'artifacts/bin/OnlyWinget/Release'
$otherRoot = Join-Path $fixtureRoot 'portable'
New-Item -ItemType Directory -Path $ownedRoot, $otherRoot -Force | Out-Null
$sourcePath = Join-Path $fixtureRoot 'Window.cs'
$ownedPath = Join-Path $ownedRoot 'OnlyWinget.exe'
$otherPath = Join-Path $otherRoot 'OnlyWinget.exe'
$installerSource = Join-Path $fixtureRoot 'fixture.nsi'
$installerPath = Join-Path $fixtureRoot 'fixture.exe'
$processes = [Collections.Generic.List[Diagnostics.Process]]::new()

function Start-FixtureWindow {
    param([string]$Path, [switch]$RefuseClose)
    $arguments = if ($RefuseClose) { 'refuse' } else { 'accept' }
    $process = Start-Process -FilePath $Path -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $processes.Add($process)
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        $process.Refresh()
        if ($process.HasExited) { throw 'Fixture window exited before readiness.' }
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) { return $process }
        Start-Sleep -Milliseconds 50
    } while ([DateTime]::UtcNow -lt $deadline)
    throw 'Fixture window was not ready.'
}

try {
    @'
using System;
using System.Windows.Forms;
class Program {
    [STAThread] static void Main(string[] args) {
        var window = new Form { Text = "OnlyWinget ownership fixture", WindowState = FormWindowState.Minimized };
        if (args[0] == "refuse") window.FormClosing += (sender, e) => e.Cancel = true;
        Application.Run(window);
    }
}
'@ | Set-Content -LiteralPath $sourcePath -Encoding utf8
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    & $compiler /nologo /target:winexe /reference:System.Windows.Forms.dll "/out:$ownedPath" $sourcePath
    if ($LASTEXITCODE -ne 0) { throw 'Window fixture compilation failed.' }
    Copy-Item -LiteralPath $ownedPath -Destination $otherPath
    $other = Start-FixtureWindow -Path $otherPath
    $owned = Start-FixtureWindow -Path $ownedPath
    $script:OnlyWingetRepositoryRoot = $fixtureRoot
    $blocked = $false
    try { Assert-ExecutableNotLocked -ActionName 'Fixture build' } catch {
        if ($_.Exception.Message -notmatch "PID: $($owned.Id)") { throw }
        $blocked = $true
    }
    if (-not $blocked) { throw 'Owned output lock was not detected.' }
    Assert-ExecutableNotLocked -KillProcess -ActionName 'Fixture build'
    if (-not $owned.HasExited -or $other.HasExited) { throw 'Script close ownership failed.' }
    Assert-ExecutableNotLocked -ActionName 'Fixture build with unrelated instance'

    $include = Join-Path $repositoryRoot 'src/OnlyWinget.Setup/CloseOwnedApplication.nsh'
    $closeScript = Join-Path $PSScriptRoot 'support/CloseOwnedApplication.ps1'
    @'
Unicode true
RequestExecutionLevel user
Name "OnlyWinget shutdown fixture"
OutFile "${TEST_SETUP}"
!include "${CLOSE_INCLUDE}"
Section
  WriteUninstaller "$INSTDIR\Uninstall.exe"
SectionEnd
Section "Uninstall"
  !insertmacro OnlyWingetCloseInstallation
  Delete "$INSTDIR\marker.txt"
SectionEnd
'@ | Set-Content -LiteralPath $installerSource -Encoding utf8
    $makensis = 'C:/Program Files (x86)/NSIS/makensis.exe'
    & $makensis "/DTEST_SETUP=$installerPath" "/DCLOSE_INCLUDE=$include" "/DONLYWINGET_CLOSE_SCRIPT=$closeScript" $installerSource > $null
    if ($LASTEXITCODE -ne 0) { throw 'Shutdown fixture compilation failed.' }
    $setup = Start-Process -FilePath $installerPath -ArgumentList '/S', "/D=$ownedRoot" -WindowStyle Hidden -Wait -PassThru
    if ($setup.ExitCode -ne 0) { throw 'Shutdown fixture installation failed.' }
    $uninstaller = Join-Path $ownedRoot 'Uninstall.exe'
    $marker = Join-Path $ownedRoot 'marker.txt'
    foreach ($refuse in @($true, $false)) {
        $owned = Start-FixtureWindow -Path $ownedPath -RefuseClose:$refuse
        [IO.File]::WriteAllText($marker, 'preserve until successful close')
        $uninstall = Start-Process -FilePath $uninstaller -ArgumentList '/S', "_?=$ownedRoot" -WindowStyle Hidden -Wait -PassThru
        if ($refuse) {
            if ($uninstall.ExitCode -eq 0 -or $owned.HasExited -or -not (Test-Path -LiteralPath $marker)) {
                throw 'Refused close did not preserve process and installation.'
            }
            # This fixture was started by this test and deliberately refuses WM_CLOSE.
            $owned.Kill(); $owned.WaitForExit()
        } else {
            if ($uninstall.ExitCode -ne 0 -or -not $owned.WaitForExit(5000) -or (Test-Path -LiteralPath $marker)) {
                throw "Owned uninstaller close failed (exit: $($uninstall.ExitCode), exited: $($owned.HasExited), marker: $(Test-Path -LiteralPath $marker))."
            }
        }
        if ($other.HasExited) { throw 'Uninstaller closed an unrelated portable instance.' }
    }
    Write-Host 'PASS: script/NSIS close only owned executable; unrelated copies survive; refused close preserves installation.'
}
finally {
    $script:OnlyWingetRepositoryRoot = $repositoryRoot
    foreach ($process in $processes) {
        if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
        $process.Dispose()
    }
    $resolvedRoot = [IO.Path]::GetFullPath($fixtureRoot)
    if (-not $resolvedRoot.StartsWith((Join-Path $repositoryRoot 'artifacts/process-tests/'), [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Unexpected fixture cleanup path.'
    }
    if (Test-Path -LiteralPath $resolvedRoot) { Remove-Item -LiteralPath $resolvedRoot -Recurse -Force }
}
