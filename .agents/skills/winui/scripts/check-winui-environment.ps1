# Check WinUI Environment and Project Structure
# Read-only diagnostic script for OnlyWinget WinUI 3 skill

[CmdletBinding()]
param()

$ErrorActionPreference = 'SilentlyContinue'
Set-StrictMode -Version Latest

Write-Host '==================================================' -ForegroundColor Cyan
Write-Host '     OnlyWinget WinUI 3 & .NET Diagnostics' -ForegroundColor Cyan
Write-Host '==================================================' -ForegroundColor Cyan

$cwd = Get-Location

# 1. OS & PowerShell Info
$osName = (Get-CimInstance Win32_OperatingSystem).Caption
$osVersion = [System.Environment]::OSVersion.Version.ToString()
Write-Host '[OS & Shell]' -ForegroundColor Yellow
Write-Host "  OS: $osName ($osVersion)"
Write-Host "  PowerShell: $($PSVersionTable.PSVersion) ($($PSVersionTable.PSEdition))"
Write-Host ''

# 2. .NET SDK & global.json Pin Check
Write-Host '[.NET SDK & Runtimes]' -ForegroundColor Yellow
$globalJsonPath = Join-Path $cwd.Path 'global.json'
if (Test-Path -LiteralPath $globalJsonPath) {
    $globalJson = Get-Content -LiteralPath $globalJsonPath -Raw | ConvertFrom-Json
    Write-Host "  global.json SDK pin: $($globalJson.sdk.version) (rollForward: $($globalJson.sdk.rollForward))"
}
$sdkVersion = (& dotnet --version 2>$null)
if ($LASTEXITCODE -eq 0 -and $sdkVersion) {
    Write-Host "  Active .NET SDK: $($sdkVersion.Trim())"
    Write-Host '  Installed .NET SDKs:'
    & dotnet --list-sdks | ForEach-Object { Write-Host "    - $_" }
    Write-Host '  Installed Runtimes:'
    & dotnet --list-runtimes | Where-Object { $_ -match 'WindowsDesktop|NETCore' } | ForEach-Object { Write-Host "    - $_" }
} else {
    Write-Host '  .NET CLI not found or errored.' -ForegroundColor Red
}
Write-Host ''

# 3. External Tooling (winget, NSIS, winapp, MSBuild)
Write-Host '[External Tooling]' -ForegroundColor Yellow
$wingetCmd = Get-Command winget -ErrorAction SilentlyContinue
if ($wingetCmd) {
    $wingetVer = (& winget --version 2>$null)
    Write-Host "  winget: $($wingetVer.Trim()) ($($wingetCmd.Source))"
} else {
    Write-Host '  winget: not found in PATH' -ForegroundColor Yellow
}

$makensisCandidates = @(
    (Get-Command makensis -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
    (Join-Path ${env:ProgramFiles(x86)} 'NSIS\makensis.exe'),
    (Join-Path $env:ProgramFiles 'NSIS\makensis.exe')
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -Unique
if ($makensisCandidates) {
    $nsisExe = $makensisCandidates | Select-Object -First 1
    $nsisVer = (& $nsisExe /VERSION 2>$null)
    Write-Host "  NSIS: $nsisVer ($nsisExe)"
} else {
    Write-Host '  NSIS (makensis.exe): not found' -ForegroundColor Yellow
}

$winappCmd = Get-Command winapp -ErrorAction SilentlyContinue
if ($winappCmd) {
    Write-Host "  winapp CLI: $($winappCmd.Source)"
} else {
    Write-Host '  winapp CLI: not found in PATH (optional; required only for scripts/ui-test.ps1)'
}

$vswherePath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (Test-Path -LiteralPath $vswherePath) {
    $vsInstances = @(& $vswherePath -latest -products * -requires Microsoft.Component.MSBuild -format json | ConvertFrom-Json)
    if ($vsInstances.Count -gt 0) {
        Write-Host "  VS MSBuild: $($vsInstances[0].displayName) ($($vsInstances[0].installationVersion))"
    }
}
Write-Host ''

# 4. Project Workspace Scan
Write-Host "[Workspace Analysis: $($cwd.Path)]" -ForegroundColor Yellow
$slnFiles = @(Get-ChildItem -Path $cwd.Path -Filter *.sln -Recurse -Depth 2 | Where-Object { $_.FullName -notmatch '[\\/](artifacts|bin|obj|\.git)[\\/]' })
$csprojFiles = @(Get-ChildItem -Path $cwd.Path -Filter *.csproj -Recurse -Depth 3 | Where-Object { $_.FullName -notmatch '[\\/](artifacts|bin|obj|\.git)[\\/]' })

Write-Host '  Solutions found:'
$slnFiles | ForEach-Object { Write-Host "    - $($_.FullName)" }

Write-Host '  Projects found:'
$csprojFiles | ForEach-Object { Write-Host "    - $($_.FullName)" }
Write-Host ''

# 5. Analyze Csproj Details
Write-Host '[Project Dependency & Configuration Scan]' -ForegroundColor Yellow
foreach ($proj in $csprojFiles) {
    Write-Host "  Project: $($proj.Name)" -ForegroundColor Green
    [xml]$xml = Get-Content -LiteralPath $proj.FullName

    $propGroups = @($xml.Project.PropertyGroup)
    $targetFramework = ($propGroups | ForEach-Object { $_['TargetFramework'] } | Where-Object { $_ } | Select-Object -First 1)
    if ($targetFramework) {
        Write-Host "    Target Framework: $($targetFramework.InnerText)"
    }

    $useWinUI = ($propGroups | ForEach-Object { $_['UseWinUI'] } | Where-Object { $_ } | Select-Object -First 1)
    if ($useWinUI -and $useWinUI.InnerText -eq 'true') {
        $packageType = ($propGroups | ForEach-Object { $_['WindowsPackageType'] } | Where-Object { $_ } | Select-Object -First 1)
        if ($packageType) {
            Write-Host "    Windows Package Type: $($packageType.InnerText)"
        }
        $selfContained = ($propGroups | ForEach-Object { $_['WindowsAppSDKSelfContained'] } | Where-Object { $_ } | Select-Object -First 1)
        if ($selfContained) {
            Write-Host "    Self-Contained WindowsAppSDK: $($selfContained.InnerText)"
        }
    }

    $packageReferences = @($xml.SelectNodes('//PackageReference[@Include]'))
    if ($packageReferences.Count -gt 0) {
        Write-Host '    Package References:'
        foreach ($pkg in $packageReferences) {
            Write-Host "      - $($pkg.GetAttribute('Include')) (v$($pkg.GetAttribute('Version')))"
        }
    }
}
Write-Host ''

# 6. Scan for legacy UWP UI references (Windows.UI.Xaml) in source/tests
Write-Host '[Legacy Code Check]' -ForegroundColor Yellow
$sourceFiles = @(Get-ChildItem -Path $cwd.Path -Recurse -File -Include *.cs, *.xaml | Where-Object { $_.FullName -notmatch '[\\/](artifacts|bin|obj|\.git)[\\/]' })
$legacyReferences = @($sourceFiles | Select-String -Pattern 'Windows\.UI\.Xaml')
if ($legacyReferences.Count -gt 0) {
    Write-Host "  WARNING: Found legacy 'Windows.UI.Xaml' namespaces in $($legacyReferences.Count) occurrences!" -ForegroundColor Red
    $legacyReferences | Select-Object -First 5 | ForEach-Object {
        Write-Host "    - $($_.Path) (Line $($_.LineNumber)): $($_.Line.Trim())"
    }
} else {
    Write-Host "  Success: No legacy 'Windows.UI.Xaml' namespaces detected." -ForegroundColor Green
}
Write-Host ''

# 7. Check source manifest files
Write-Host '[App Manifests]' -ForegroundColor Yellow
$manifests = @(Get-ChildItem -Path $cwd.Path -Recurse -File -Include *package.appxmanifest, *app.manifest | Where-Object { $_.FullName -notmatch '[\\/](artifacts|bin|obj|\.git)[\\/]' })
if ($manifests.Count -gt 0) {
    foreach ($m in $manifests) {
        Write-Host "  Found: $($m.FullName.Substring($cwd.Path.Length))"
    }
} else {
    Write-Host '  No app manifests found.'
}
Write-Host ''

Write-Host '==================================================' -ForegroundColor Cyan
Write-Host 'Diagnostics Complete' -ForegroundColor Cyan
Write-Host '==================================================' -ForegroundColor Cyan
