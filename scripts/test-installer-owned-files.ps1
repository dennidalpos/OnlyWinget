$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/InstallerFiles.ps1')

$makensis = 'C:\Program Files (x86)\NSIS\makensis.exe'
if (-not (Test-Path -LiteralPath $makensis)) {
    $makensis = (Get-Command makensis -ErrorAction Stop).Source
}
$repoRoot = Split-Path $PSScriptRoot -Parent
$testRoot = Join-Path $repoRoot ('artifacts/installer-file-tests/' + [Guid]::NewGuid().ToString('N'))
$publishDirectory = Join-Path $testRoot 'publish'
$nestedDirectory = Join-Path $publishDirectory 'nested'
$installDirectory = Join-Path $testRoot 'install'
$installedNestedDirectory = Join-Path $installDirectory 'nested'
New-Item -ItemType Directory -Path $nestedDirectory, $installedNestedDirectory -Force | Out-Null
$ownedRootFile = Join-Path $publishDirectory 'owned.txt'
$ownedNestedFile = Join-Path $nestedDirectory 'owned $literal.txt'
Set-Content -LiteralPath $ownedRootFile -Value 'owned' -Encoding utf8
Set-Content -LiteralPath $ownedNestedFile -Value 'owned nested' -Encoding utf8
$unrelatedRootFile = Join-Path $installDirectory 'user.txt'
$unrelatedNestedFile = Join-Path $installedNestedDirectory 'user.txt'
Set-Content -LiteralPath $unrelatedRootFile -Value 'preserve root' -Encoding utf8
Set-Content -LiteralPath $unrelatedNestedFile -Value 'preserve nested' -Encoding utf8
$includePath = Join-Path $testRoot 'InstalledFiles.nsh'
Write-InstallerFileInclude -PublishDirectory $publishDirectory -OutputPath $includePath
$setupPath = Join-Path $testRoot 'fixture.exe'
$scriptPath = Join-Path $testRoot 'fixture.nsi'
@'
Unicode true
RequestExecutionLevel user
Name "OnlyWinget ownership regression"
OutFile "${TEST_SETUP}"
!include "${TEST_INCLUDE}"
Section
  !insertmacro OnlyWingetInstallFiles
  WriteUninstaller "$INSTDIR\Uninstall.exe"
SectionEnd
Section "Uninstall"
  SetOutPath "$TEMP"
  !insertmacro OnlyWingetUninstallFiles
  RMDir "$INSTDIR"
SectionEnd
'@ | Set-Content -LiteralPath $scriptPath -Encoding utf8

try {
    & $makensis "/DPUBLISH_DIR=$publishDirectory" "/DTEST_INCLUDE=$includePath" "/DTEST_SETUP=$setupPath" $scriptPath > $null
    if ($LASTEXITCODE -ne 0) { throw 'NSIS ownership fixture compilation failed.' }
    $installProcess = Start-Process -FilePath $setupPath -ArgumentList '/S', "/D=$installDirectory" -WindowStyle Hidden -Wait -PassThru
    if ($installProcess.ExitCode -ne 0) { throw 'NSIS ownership fixture installation failed.' }
    $uninstallerPath = Join-Path $installDirectory 'Uninstall.exe'
    $uninstallProcess = Start-Process -FilePath $uninstallerPath -ArgumentList '/S', "_?=$installDirectory" -WindowStyle Hidden -Wait -PassThru
    if ($uninstallProcess.ExitCode -ne 0) { throw 'NSIS ownership fixture uninstallation failed.' }
    foreach ($installedFile in @((Join-Path $installDirectory 'owned.txt'), (Join-Path $installedNestedDirectory 'owned $literal.txt'))) {
        if (Test-Path -LiteralPath $installedFile) { throw "Installer-owned file was not removed: $installedFile" }
    }
    if ((Get-Content -LiteralPath $unrelatedRootFile -Raw).Trim() -ne 'preserve root' -or
        (Get-Content -LiteralPath $unrelatedNestedFile -Raw).Trim() -ne 'preserve nested') {
        throw 'Unrelated files were modified or deleted.'
    }
    Write-Host 'PASS: NSIS removes owned files and preserves unrelated root/nested files.'
}
finally {
    # Remove only the exact fixture files created by this test, then empty directories.
    foreach ($file in @($ownedRootFile, $ownedNestedFile, $unrelatedRootFile, $unrelatedNestedFile, $includePath, $setupPath, $scriptPath, (Join-Path $installDirectory 'Uninstall.exe'))) {
        if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file -Force -ErrorAction Stop }
    }
    foreach ($directory in @($nestedDirectory, $publishDirectory, $installedNestedDirectory, $installDirectory, $testRoot)) {
        if (Test-Path -LiteralPath $directory) { Remove-Item -LiteralPath $directory -ErrorAction Stop }
    }
}
