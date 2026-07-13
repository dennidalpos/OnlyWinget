# Check WinUI Environment and Project Structure
# Diagnostic script for Antigravity winui skill

$ErrorActionPreference = "SilentlyContinue"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "     WinUI 3 & .NET Environment Diagnostics" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# 1. OS & Windows Version Check
$osName = (Get-CimInstance Win32_OperatingSystem).Caption
$osVersion = [System.Environment]::OSVersion.Version.ToString()
Write-Host "[OS Info]" -ForegroundColor Yellow
Write-Host "  Name: $osName"
Write-Host "  Version: $osVersion"
Write-Host ""

# 2. .NET SDK Info
Write-Host "[.NET SDK & Runtimes]" -ForegroundColor Yellow
$dotnetInfo = dotnet --info
if ($LASTEXITCODE -eq 0) {
    # Extract version info
    $sdkVersion = (dotnet --version).Trim()
    Write-Host "  Active .NET SDK: $sdkVersion"
    
    # List installed SDKs
    Write-Host "  Installed .NET SDKs:"
    dotnet --list-sdks | ForEach-Object { Write-Host "    - $_" }
    
    # List installed Runtimes
    Write-Host "  Installed Runtimes:"
    dotnet --list-runtimes | Where-Object { $_ -match "WindowsDesktop|NETCore" } | ForEach-Object { Write-Host "    - $_" }
} else {
    Write-Host "  .NET CLI not found or errored." -ForegroundColor Red
}
Write-Host ""

# 3. .NET Workloads Check
Write-Host "[.NET Workloads]" -ForegroundColor Yellow
$workloads = dotnet workload list
if ($LASTEXITCODE -eq 0) {
    $workloads | ForEach-Object { Write-Host "  $_" }
} else {
    Write-Host "  No workloads found or dotnet workload failed."
}
Write-Host ""

# 4. Search for VS Build Tools & MSBuild
Write-Host "[MSBuild / Visual Studio]" -ForegroundColor Yellow
$vswherePath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (Test-Path $vswherePath) {
    $vsInstances = & $vswherePath -latest -products * -requires Microsoft.Component.MSBuild -format json | ConvertFrom-Json
    if ($vsInstances) {
        Write-Host "  VS Instance: $($vsInstances.displayName) ($($vsInstances.installationVersion))"
        Write-Host "  Path: $($vsInstances.installationPath)"
    } else {
        Write-Host "  No Visual Studio MSBuild installation discovered by vswhere."
    }
} else {
    Write-Host "  vswhere.exe not found at default location."
}
Write-Host ""

# 5. Project Workspace Scan
$cwd = Get-Location
Write-Host "[Workspace Analysis: $cwd]" -ForegroundColor Yellow
$slnFiles = Get-ChildItem -Filter *.sln -Recurse -Depth 2
$csprojFiles = Get-ChildItem -Filter *.csproj -Recurse -Depth 3

Write-Host "  Solutions found:"
$slnFiles | ForEach-Object { Write-Host "    - $_" }

Write-Host "  Projects found:"
$csprojFiles | ForEach-Object { Write-Host "    - $_" }
Write-Host ""

# 6. Analyze Csproj Details (Target Frameworks, SDK references, etc)
Write-Host "[Project Dependency & Configuration Scan]" -ForegroundColor Yellow
foreach ($proj in $csprojFiles) {
    Write-Host "  Project: $($proj.Name)" -ForegroundColor Green
    [xml]$xml = Get-Content $proj.FullName
    
    # Check Target Framework
    $targetFramework = $xml.Project.PropertyGroup.TargetFramework
    if ($targetFramework) {
        Write-Host "    Target Framework: $targetFramework"
    }
    
    # Check WindowsPackageType (Packaged vs Unpackaged)
    $packageType = $xml.Project.PropertyGroup.WindowsPackageType
    if ($packageType) {
        Write-Host "    Windows Package Type: $packageType (Unpackaged = 'None')"
    } else {
        Write-Host "    Windows Package Type: [Not Specified] (Default Packaged)"
    }
    
    # Check SDK Self Contained
    $selfContained = $xml.Project.PropertyGroup.WindowsAppSDKSelfContained
    if ($selfContained) {
        Write-Host "    Self-Contained WindowsAppSDK: $selfContained"
    }

    # Package References (WindowsAppSDK, CsWinRT, CommunityToolkit)
    $packageReferences = $xml.Project.ItemGroup.PackageReference
    if ($packageReferences) {
        Write-Host "    Package References:"
        foreach ($pkg in $packageReferences) {
            Write-Host "      - $($pkg.Include) (v$($pkg.Version))"
        }
    }
}
Write-Host ""

# 7. Scan for legacy UWP UI references (Windows.UI.Xaml)
Write-Host "[Legacy Code Check]" -ForegroundColor Yellow
$legacyReferences = Get-ChildItem -Recurse -Include *.cs,*.xaml -Exclude bin,obj,artifacts | Select-String "Windows.UI.Xaml"
if ($legacyReferences) {
    Write-Host "  WARNING: Found legacy 'Windows.UI.Xaml' namespaces in $($legacyReferences.Count) occurrences!" -ForegroundColor Red
    $legacyReferences | Select-Object -First 5 | ForEach-Object {
        Write-Host "    - $($_.Path) (Line $($_.LineNumber)): $($_.Line.Trim())"
    }
    if ($legacyReferences.Count -gt 5) {
        Write-Host "    ... and $($legacyReferences.Count - 5) more."
    }
} else {
    Write-Host "  Success: No legacy 'Windows.UI.Xaml' namespaces detected." -ForegroundColor Green
}
Write-Host ""

# 8. Check manifest files
Write-Host "[App Manifests]" -ForegroundColor Yellow
$manifests = Get-ChildItem -Recurse -Include *package.appxmanifest, *app.manifest -Exclude bin,obj,artifacts
if ($manifests) {
    foreach ($m in $manifests) {
        Write-Host "  Found: $($m.FullName.Replace($cwd.Path, ''))"
    }
} else {
    Write-Host "  No app manifests found."
}
Write-Host ""

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "Diagnostics Complete" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
