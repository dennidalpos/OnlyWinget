param([switch]$DiscoveryOnly)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/ScriptHelpers.ps1')
. (Join-Path $PSScriptRoot 'support/CloseOwnedApplication.ps1')

# MainModule failures cannot be reproduced reliably through elevation on every host.
# Model the native access failures and null module explicitly, under both PS runtimes.
Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
public sealed class OwnershipProcessModule {
    public string FileName { get; set; }
}
public sealed class OwnershipProcessCandidate : IDisposable {
    private readonly string scenario;
    private readonly string path;
    public bool Disposed { get; private set; }
    public OwnershipProcessCandidate(string scenario, string path) { this.scenario = scenario; this.path = path; }
    public bool HasExited {
        get {
            if (scenario == "status-denied") throw new Win32Exception(5);
            return scenario == "exited";
        }
    }
    public object MainModule {
        get {
            switch (scenario) {
                case "null": return null;
                case "missing-property": return new object();
                case "denied": throw new Win32Exception(5);
                case "unauthorized": throw new UnauthorizedAccessException();
                case "exit-race": throw new InvalidOperationException();
                case "unexpected": throw new NotSupportedException("Unexpected fixture failure");
                default: return new OwnershipProcessModule { FileName = path };
            }
        }
    }
    public void Dispose() { Disposed = true; }
}
'@

$discoveryPath = Join-Path $PSScriptRoot 'OnlyWinget.exe'
$ownedCandidate = [OwnershipProcessCandidate]::new('owned', $discoveryPath)
$ignoredCandidates = @(
    foreach ($scenario in @('null', 'missing-property', 'denied', 'unauthorized', 'exit-race', 'status-denied', 'exited')) {
        [OwnershipProcessCandidate]::new($scenario, $discoveryPath)
    }
    [OwnershipProcessCandidate]::new('empty', '')
    [OwnershipProcessCandidate]::new('unrelated', (Join-Path $PSScriptRoot 'portable/OnlyWinget.exe'))
)
try {
    $found = @(Get-OwnedApplicationProcess -ExecutablePaths @($discoveryPath) -CandidateProcesses ($ignoredCandidates + $ownedCandidate))
    if ($found.Count -ne 1 -or -not [object]::ReferenceEquals($found[0], $ownedCandidate)) {
        throw 'Discovery did not retain exactly the verified owned process.'
    }
    if ($ownedCandidate.Disposed -or @($ignoredCandidates | Where-Object { -not $_.Disposed }).Count -ne 0) {
        throw 'Discovery did not dispose ignored candidates or disposed the returned process.'
    }
    $unexpected = [OwnershipProcessCandidate]::new('unexpected', $discoveryPath)
    $failed = $false
    try { Get-OwnedApplicationProcess -ExecutablePaths @($discoveryPath) -CandidateProcesses @($unexpected) } catch {
        if ($_.Exception.ToString() -notmatch 'Unexpected fixture failure') { throw }
        $failed = $true
    }
    if (-not $failed -or -not $unexpected.Disposed) { throw 'Unexpected failures must propagate and dispose the candidate.' }
} finally { $ownedCandidate.Dispose() }
Write-Host 'PASS: inaccessible/null/exited candidates ignored and disposed; exact ownership retained; unexpected failures propagate.'
if ($DiscoveryOnly) { return }

$windowsPowerShell = Join-Path $env:WINDIR 'System32/WindowsPowerShell/v1.0/powershell.exe'
& $windowsPowerShell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $PSCommandPath -DiscoveryOnly
if ($LASTEXITCODE -ne 0) { throw 'Windows PowerShell discovery regression failed.' }

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

    $nsiPath = Join-Path $repositoryRoot 'src/OnlyWinget.Setup/OnlyWinget.nsi'
    $nsiContent = [IO.File]::ReadAllText($nsiPath)
    if ($nsiContent -notmatch '(?s)Section\s+"MainSection"\s+SEC01\r?\n(?:(?!SectionEnd).)*?!insertmacro\s+OnlyWingetCloseInstallation\r?\n(?:(?!SectionEnd).)*?!insertmacro\s+OnlyWingetInstallFiles\r?\n.*?SectionEnd') {
        throw 'OnlyWinget.nsi MainSection must invoke OnlyWingetCloseInstallation before OnlyWingetInstallFiles.'
    }
    if ($nsiContent -notmatch '(?s)Section\s+"Uninstall"\r?\n(?:(?!SectionEnd).)*?!insertmacro\s+OnlyWingetCloseInstallation\r?\n(?:(?!SectionEnd).)*?!insertmacro\s+OnlyWingetUninstallFiles\r?\n.*?SectionEnd') {
        throw 'OnlyWinget.nsi Uninstall section must invoke OnlyWingetCloseInstallation before OnlyWingetUninstallFiles.'
    }

    $include = Join-Path $repositoryRoot 'src/OnlyWinget.Setup/CloseOwnedApplication.nsh'
    $closeScript = Join-Path $PSScriptRoot 'support/CloseOwnedApplication.ps1'
    @'
Unicode true
RequestExecutionLevel user
Name "OnlyWinget shutdown fixture"
OutFile "${TEST_SETUP}"
!include "${CLOSE_INCLUDE}"
Section
  !insertmacro OnlyWingetCloseInstallation
  SetOutPath "$INSTDIR"
  FileOpen $2 "$INSTDIR\install-marker.txt" w
  FileWrite $2 "installed"
  FileClose $2
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
    $installMarker = Join-Path $ownedRoot 'install-marker.txt'
    foreach ($refuse in @($true, $false)) {
        $owned = Start-FixtureWindow -Path $ownedPath -RefuseClose:$refuse
        [IO.File]::WriteAllText($installMarker, 'preserve existing installation until successful close')
        $setup = Start-Process -FilePath $installerPath -ArgumentList '/S', "/D=$ownedRoot" -WindowStyle Hidden -Wait -PassThru
        if ($refuse) {
            if ($setup.ExitCode -eq 0 -or $owned.HasExited -or [IO.File]::ReadAllText($installMarker) -ne 'preserve existing installation until successful close') {
                throw 'Refused close during install did not preserve process and existing installation.'
            }
            # This fixture was started by this test and deliberately refuses WM_CLOSE.
            $owned.Kill(); $owned.WaitForExit()
        } else {
            if ($setup.ExitCode -ne 0 -or -not $owned.WaitForExit(5000) -or [IO.File]::ReadAllText($installMarker) -ne 'installed') {
                throw "Owned installer close failed (exit: $($setup.ExitCode), exited: $($owned.HasExited), marker: $([IO.File]::ReadAllText($installMarker)))."
            }
        }
        if ($other.HasExited) { throw 'Installer closed an unrelated portable instance.' }
    }
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
    Write-Host 'PASS: script/NSIS install and uninstall close only owned executable; unrelated copies survive; refused close preserves installation.'
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
