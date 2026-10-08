$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/PackageArtifacts.ps1')

$repoRoot = Split-Path $PSScriptRoot -Parent
$fixtureRoot = Join-Path $repoRoot ('artifacts/package-tests/' + [Guid]::NewGuid().ToString('N'))
$output = Join-Path $fixtureRoot 'dist'
$publish = Join-Path $fixtureRoot 'publish'
$stage = Join-Path $fixtureRoot 'stage'
New-Item -ItemType Directory -Path $output, $publish, $stage -Force | Out-Null
$names = @('OnlyWinget-1.0.2-setup.exe', 'OnlyWinget-1.0.2-portable-x64.zip')
$setup = Join-Path $output $names[0]
$portable = Join-Path $output $names[1]
$payload = Join-Path $publish 'OnlyWinget.exe'
$hidden = Join-Path $publish 'hidden.txt'
[IO.File]::WriteAllText($payload, 'isolated app payload')
[IO.File]::WriteAllText($hidden, 'hidden payload')
[IO.File]::SetAttributes($hidden, [IO.FileAttributes]::Hidden)
$unrelated = Join-Path $output 'unrelated.txt'
[IO.File]::WriteAllText($unrelated, 'preserve')
$child = $null
$lock = $null

function Reset-ReleaseSentinel {
    [IO.File]::WriteAllText($setup, 'previous setup')
    [IO.File]::WriteAllText($portable, 'previous portable')
}

function Assert-ReleaseSentinel {
    if ([IO.File]::ReadAllText($setup) -ne 'previous setup' -or [IO.File]::ReadAllText($portable) -ne 'previous portable') {
        throw 'Previous distribution artifacts were changed on failure.'
    }
    if ([IO.File]::ReadAllText($unrelated) -ne 'preserve') { throw 'Unrelated distribution file was changed.' }
}

function Assert-Failure {
    param([scriptblock]$Action, [string]$Message, [string]$ExpectedError)
    $failed = $false
    try { & $Action } catch {
        if ($_.Exception.Message -notmatch $ExpectedError) { throw }
        $failed = $true
    }
    if (-not $failed) { throw $Message }
}

# A real minimal NSIS executable is used; no fixture code enters the app payload.
$makensis = 'C:\Program Files (x86)\NSIS\makensis.exe'
if (-not (Test-Path -LiteralPath $makensis)) { $makensis = (Get-Command makensis -ErrorAction Stop).Source }
$fixtureScript = Join-Path $fixtureRoot 'fixture.nsi'
@'
Unicode true
RequestExecutionLevel user
Name "OnlyWinget packaging fixture"
OutFile "${TEST_SETUP}"
Section
SectionEnd
'@ | Set-Content -LiteralPath $fixtureScript -Encoding utf8
$build = {
    param($destination)
    & $makensis "/DTEST_SETUP=$destination" $fixtureScript > $null
    if ($LASTEXITCODE -ne 0) { throw 'NSIS fixture failed.' }
}

try {
    Reset-ReleaseSentinel
    Assert-Failure -Message 'Expected NSIS failure.' -ExpectedError 'Injected NSIS compiler failure' -Action {
        New-PackageArtifactPair -OutputDirectory $output -PublishDirectory $publish -Version '1.0.2' -BuildSetup {
            param($destination)
            [IO.File]::WriteAllText($destination, 'partial NSIS output')
            throw 'Injected NSIS compiler failure.'
        }
    }
    Assert-ReleaseSentinel

    $lock = [IO.File]::Open($payload, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
    try {
        Assert-Failure -Message 'Expected compression failure.' -ExpectedError 'used by another process|being used|utilizzato da un altro processo' -Action {
            New-PackageArtifactPair -OutputDirectory $output -PublishDirectory $publish -Version '1.0.2' -BuildSetup $build
        }
    } finally { $lock.Dispose(); $lock = $null }
    Assert-ReleaseSentinel

    Assert-Failure -Message 'Expected invalid setup rejection.' -ExpectedError 'Invalid staged setup executable' -Action {
        New-PackageArtifactPair -OutputDirectory $output -PublishDirectory $publish -Version '1.0.2' -BuildSetup {
            param($destination)
            [IO.File]::WriteAllText($destination, 'invalid executable')
        }
    }
    Assert-ReleaseSentinel

    # Fail after the first file was promoted; the unchanged locked ZIP remains readable.
    $lock = [IO.File]::Open($portable, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        Assert-Failure -Message 'Expected promotion sharing violation.' -ExpectedError 'used by another process|being used|utilizzato da un altro processo' -Action {
            New-PackageArtifactPair -OutputDirectory $output -PublishDirectory $publish -Version '1.0.2' -BuildSetup $build
        }
    } finally { $lock.Dispose(); $lock = $null }
    Assert-ReleaseSentinel

    # Stop only our child after its real journal/first replacement checkpoint.
    $childScript = Join-Path $fixtureRoot 'interrupt.ps1'
    $ready = Join-Path $fixtureRoot 'ready'
    @'
param($Helper, $Output, $Stage, $Ready)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. $Helper
$names = @('OnlyWinget-1.0.2-setup.exe', 'OnlyWinget-1.0.2-portable-x64.zip')
Initialize-PackagePromotion -OutputDirectory $Output -Names $names
$replacement = Join-Path $Stage $names[0]
[IO.File]::WriteAllText($replacement, 'new interrupted setup')
[IO.File]::Replace($replacement, (Join-Path $Output $names[0]), [NullString]::Value)
[IO.File]::WriteAllText($Ready, 'checkpoint')
Wait-Event -Timeout 60 | Out-Null
'@ | Set-Content -LiteralPath $childScript -Encoding utf8
    $helper = Join-Path $PSScriptRoot 'support/PackageArtifacts.ps1'
    $arguments = @('-NoProfile', '-File', ('"{0}"' -f $childScript), ('"{0}"' -f $helper), ('"{0}"' -f $output), ('"{0}"' -f $stage), ('"{0}"' -f $ready))
    $child = Start-Process -FilePath (Get-Process -Id $PID).Path -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    while (-not (Test-Path -LiteralPath $ready) -and -not $child.HasExited -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 50 }
    if (-not (Test-Path -LiteralPath $ready)) { throw 'Interrupted-package checkpoint not reached.' }
    $child.Kill(); $child.WaitForExit()
    Repair-PackagePromotion -OutputDirectory $output
    Repair-PackagePromotion -OutputDirectory $output
    Assert-ReleaseSentinel

    # A damaged journal must fail closed without changing outputs or deleting evidence.
    $recovery = Join-Path $output '.package-recovery'
    New-Item -ItemType Directory -Path $recovery | Out-Null
    $statePath = Join-Path $recovery 'state.json'
    [IO.File]::WriteAllText($statePath, '{ invalid JSON')
    Assert-Failure -Message 'Expected corrupt recovery rejection.' -ExpectedError 'JSON|Invalid character|Unexpected character' -Action { Repair-PackagePromotion -OutputDirectory $output }
    Assert-ReleaseSentinel
    if (-not (Test-Path -LiteralPath $statePath)) { throw 'Corrupt recovery journal was deleted.' }
    Remove-Item -LiteralPath $statePath
    Remove-Item -LiteralPath $recovery

    New-PackageArtifactPair -OutputDirectory $output -PublishDirectory $publish -Version '1.0.2' -BuildSetup $build
    Assert-PackageArtifactPair -SetupPath $setup -PortablePath $portable -PublishDirectory $publish
    $zip = [IO.Compression.ZipFile]::OpenRead($portable)
    try { if ($null -eq $zip.GetEntry('hidden.txt')) { throw 'Hidden payload was omitted.' } } finally { $zip.Dispose() }
    if ([IO.File]::ReadAllText($unrelated) -ne 'preserve') { throw 'Unrelated file was modified.' }
    if (Test-Path -LiteralPath $recovery) { throw 'Completed recovery files remain.' }
    if (@(Get-ChildItem -LiteralPath $output -Directory -Filter '.package-stage-*').Count -ne 0) { throw 'Staging files remain.' }

    Remove-Item -LiteralPath $setup, $portable
    Initialize-PackagePromotion -OutputDirectory $output -Names $names
    [IO.File]::WriteAllText($setup, 'interrupted first publication')
    Repair-PackagePromotion -OutputDirectory $output
    if ((Test-Path -LiteralPath $setup) -or (Test-Path -LiteralPath $portable)) { throw 'Failed first publication left final output behind.' }
    New-PackageArtifactPair -OutputDirectory $output -PublishDirectory $publish -Version '1.0.2' -BuildSetup $build
    Assert-PackageArtifactPair -SetupPath $setup -PortablePath $portable -PublishDirectory $publish

    $setupHash = (Get-FileHash -LiteralPath $setup).Hash
    $portableHash = (Get-FileHash -LiteralPath $portable).Hash
    Initialize-PackagePromotion -OutputDirectory $output -Names $names
    $backup = Join-Path $recovery 'setup.previous'
    [IO.File]::WriteAllText($backup, 'damaged backup')
    Assert-Failure -Message 'Expected backup integrity rejection.' -ExpectedError 'backup missing or modified' -Action { Repair-PackagePromotion -OutputDirectory $output }
    if ((Get-FileHash -LiteralPath $setup).Hash -ne $setupHash -or (Get-FileHash -LiteralPath $portable).Hash -ne $portableHash) { throw 'Damaged-backup recovery changed valid outputs.' }
    if (-not (Test-Path -LiteralPath $statePath)) { throw 'Damaged backup evidence was removed.' }
    # Discard only this deliberately corrupted fixture after confirming preservation.
    Remove-PackageRecovery -Directory $recovery
    Write-Host 'PASS: Package failures/rollback, process-interruption recovery, corrupt journal, hidden payload, replacement and first publication.'
}
finally {
    if ($null -ne $lock) { $lock.Dispose() }
    if ($null -ne $child) { if (-not $child.HasExited) { $child.Kill(); $child.WaitForExit() }; $child.Dispose() }
    $resolved = [IO.Path]::GetFullPath($fixtureRoot)
    $allowed = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts/package-tests')) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture cleanup escaped its root.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force -ErrorAction Stop
}
