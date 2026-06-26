param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$Version,
    [string]$WindowsAppRuntimeInstallerPath = $env:ONLYWINGET_WINDOWS_APP_RUNTIME_INSTALLER,
    [switch]$NoRestore,
    [switch]$StopRunningInstance,
    [ValidateSet('x86', 'x64', 'All')]
    [string]$Architecture = 'All',
    [switch]$SkipBundle,
    [switch]$NonInteractive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/ScriptHelpers.ps1')

if (Enter-InteractiveModeIfNoParameter -BoundParameters $PSBoundParameters -ScriptRoot $PSScriptRoot -NonInteractive:$NonInteractive) {
    return
}

$repoRoot = Split-Path $PSScriptRoot -Parent
$projectPath = Join-Path $repoRoot 'src/OnlyWinget/OnlyWinget.csproj'
$appIconPath = Join-Path $repoRoot 'src/OnlyWinget/Assets/OnlyWinget.ico'
$bundleLogoPath = Join-Path $repoRoot 'src/OnlyWinget/Assets/OnlyWinget-icon.png'
$licenseRtfPath = Join-Path $repoRoot 'src/OnlyWinget.Setup/License.rtf'
$installerDialogBmpPath = Join-Path $repoRoot 'src/OnlyWinget.Setup/Assets/WixUIDialog.bmp'
$installerBannerBmpPath = Join-Path $repoRoot 'src/OnlyWinget.Setup/Assets/WixUIBanner.bmp'
$wixSourcePath = Join-Path $repoRoot 'src/OnlyWinget.Setup/OnlyWinget.Setup.wxs'
$bundleSourcePath = Join-Path $repoRoot 'src/OnlyWinget.Setup/OnlyWinget.Bundle.wxs'
$bundleThemePath = Join-Path $repoRoot 'src/OnlyWinget.Setup/BurnResponsiveTheme.xml'
$bundleThemeLocalizationPath = Join-Path $repoRoot 'src/OnlyWinget.Setup/BurnResponsiveTheme.wxl'
$artifactsPath = Join-Path $repoRoot 'artifacts'
$stagingRoot = Join-Path $artifactsPath 'installer'
$msiOutputDir = Join-Path $artifactsPath "dist/OnlyWinget/$Configuration/msi"
$setupOutputDir = Join-Path $artifactsPath "dist/OnlyWinget/$Configuration"
$upgradeCode = '{B6E2D6FC-56ED-4A5C-A766-01F3FE71D7E6}'
$bundleUpgradeCode = '{A34AF980-F5F1-4E4D-8124-8DC5E889C74D}'
$builtMsiPaths = @{}
$suppressedValidationIces = @('ICE61')
$resolvedWindowsAppRuntimeInstallerX86Path = $null
$resolvedWindowsAppRuntimeInstallerX64Path = $null
$attemptedWixInstall = $false

function Add-UniquePath {
    param(
        [System.Collections.Generic.List[string]]$Paths,
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return
    }

    $normalizedPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $Paths.Contains($normalizedPath)) {
        $Paths.Add($normalizedPath)
    }
}

function Get-WixToolSearchRoot {
    $roots = [System.Collections.Generic.List[string]]::new()

    Add-UniquePath -Paths $roots -Path (Join-Path $repoRoot 'tools/wix314-binaries')

    if (-not [string]::IsNullOrWhiteSpace($env:ONLYWINGET_WIX_BIN)) {
        Add-UniquePath -Paths $roots -Path $env:ONLYWINGET_WIX_BIN
    }

    if (-not [string]::IsNullOrWhiteSpace($env:WIX)) {
        Add-UniquePath -Paths $roots -Path (Join-Path $env:WIX 'bin')
    }

    $programRoots = @(
        ${env:ProgramFiles(x86)}
        $env:ProgramFiles
    )

    foreach ($programRoot in $programRoots) {
        if ([string]::IsNullOrWhiteSpace($programRoot)) {
            continue
        }

        $toolsetRootPattern = Join-Path $programRoot 'WiX Toolset v3*'
        $toolsetRoots = @(Get-ChildItem -Path $toolsetRootPattern -Directory -ErrorAction SilentlyContinue |
            Sort-Object -Property @{
                Expression = {
                    if ($_.Name -match 'v(?<Version>\d+(?:\.\d+)*)$') {
                        return [Version]$Matches.Version
                    }

                    return [Version]'0.0'
                }
                Descending = $true
            })

        foreach ($toolsetRoot in $toolsetRoots) {
            Add-UniquePath -Paths $roots -Path (Join-Path $toolsetRoot.FullName 'bin')
        }
    }

    return $roots.ToArray()
}

function Resolve-WixTool {
    param(
        [string]$ToolName,
        [string[]]$SearchRoots
    )

    foreach ($root in $SearchRoots) {
        if ([string]::IsNullOrWhiteSpace($root)) {
            continue
        }

        $candidate = Join-Path $root $ToolName
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    $command = Get-Command $ToolName -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    if (-not $script:attemptedWixInstall) {
        $script:attemptedWixInstall = $true
        Install-WixToolset
        return Resolve-WixTool -ToolName $ToolName -SearchRoots (Get-WixToolSearchRoot)
    }

    throw "Tool WiX non trovato: $ToolName. Installa WiX Toolset 3.x, imposta ONLYWINGET_WIX_BIN alla cartella bin di WiX, aggiungi WiX al PATH, oppure aggiungi i binari in 'tools/wix314-binaries'."
}

function Resolve-WixExtension {
    param(
        [string]$ExtensionName,
        [string[]]$SearchRoots
    )

    foreach ($root in $SearchRoots) {
        if ([string]::IsNullOrWhiteSpace($root)) {
            continue
        }

        $candidate = Join-Path $root $ExtensionName
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    if (-not $script:attemptedWixInstall) {
        $script:attemptedWixInstall = $true
        Install-WixToolset
        return Resolve-WixExtension -ExtensionName $ExtensionName -SearchRoots (Get-WixToolSearchRoot)
    }

    throw "Estensione WiX non trovata: $ExtensionName."
}

function Get-ProjectVersion {
    [xml]$projectXml = Get-Content -Path $projectPath
    $rawVersion = $projectXml.Project.PropertyGroup |
        Where-Object { $_.Version } |
        Select-Object -ExpandProperty Version -First 1

    if ([string]::IsNullOrWhiteSpace($rawVersion)) {
        throw "Versione progetto non trovata in '$projectPath'. Aggiungi l'elemento <Version> al csproj oppure passa -Version."
    }

    return $rawVersion.Trim()
}

function Get-WindowsAppSdkVersion {
    [xml]$projectXml = Get-Content -Path $projectPath
    $packageReference = $projectXml.SelectSingleNode('//PackageReference[@Include="Microsoft.WindowsAppSDK"]')

    if ($null -eq $packageReference -or [string]::IsNullOrWhiteSpace($packageReference.Version)) {
        throw "Microsoft.WindowsAppSDK PackageReference non trovato in '$projectPath'."
    }

    return $packageReference.Version.Trim()
}

function Resolve-WindowsAppRuntimeInstaller {
    param(
        [string]$ExplicitPath,
        [string]$WindowsAppSdkVersion,
        [ValidateSet('x86', 'x64')]
        [string]$Architecture
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        Assert-Path -Path $ExplicitPath -Description 'Windows App Runtime installer'
        return [System.IO.Path]::GetFullPath($ExplicitPath)
    }

    $nugetRoots = [System.Collections.Generic.List[string]]::new()
    if (-not [string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) {
        Add-UniquePath -Paths $nugetRoots -Path $env:NUGET_PACKAGES
    }

    if (-not [string]::IsNullOrWhiteSpace($env:USERPROFILE)) {
        Add-UniquePath -Paths $nugetRoots -Path (Join-Path $env:USERPROFILE '.nuget/packages')
    }
    $packageNames = @('microsoft.windowsappsdk.runtime', 'microsoft.windowsappsdk.redist')

    foreach ($nugetRoot in $nugetRoots) {
        foreach ($packageName in $packageNames) {
            $versionRoot = Join-Path (Join-Path $nugetRoot $packageName) $WindowsAppSdkVersion
            if (-not (Test-Path -LiteralPath $versionRoot)) {
                continue
            }

            $candidate = Get-ChildItem -Path $versionRoot -Recurse -Filter "WindowsAppRuntimeInstall-$Architecture.exe" -File -ErrorAction SilentlyContinue |
                Select-Object -First 1
            if ($null -eq $candidate) {
                $candidate = Get-ChildItem -Path $versionRoot -Recurse -Filter 'WindowsAppRuntimeInstall.exe' -File -ErrorAction SilentlyContinue |
                    Select-Object -First 1
            }
            if ($null -ne $candidate) {
                return $candidate.FullName
            }
        }
    }

    foreach ($nugetRoot in $nugetRoots) {
        foreach ($packageName in $packageNames) {
            $packageRoot = Join-Path $nugetRoot $packageName
            if (-not (Test-Path -LiteralPath $packageRoot)) {
                continue
            }

            $candidate = Get-ChildItem -Path $packageRoot -Recurse -Filter "WindowsAppRuntimeInstall-$Architecture.exe" -File -ErrorAction SilentlyContinue |
                Sort-Object FullName -Descending |
                Select-Object -First 1
            if ($null -eq $candidate) {
                $candidate = Get-ChildItem -Path $packageRoot -Recurse -Filter 'WindowsAppRuntimeInstall.exe' -File -ErrorAction SilentlyContinue |
                    Sort-Object FullName -Descending |
                    Select-Object -First 1
            }
            if ($null -ne $candidate) {
                return $candidate.FullName
            }
        }
    }

    return Install-WindowsAppRuntimeRedist -WindowsAppSdkVersion $WindowsAppSdkVersion -Architecture $Architecture
}

function Convert-ToInstallerVersion {
    param(
        [string]$RawVersion
    )

    $sanitized = $RawVersion.Split('-', 2)[0]

    try {
        $parsedVersion = [Version]$sanitized
    }
    catch {
        throw "Versione MSI non valida: '$RawVersion'. Usa una versione numerica compatibile con Windows Installer, ad esempio 1.2.3."
    }

    $major = $parsedVersion.Major
    $minor = if ($parsedVersion.Minor -ge 0) { $parsedVersion.Minor } else { 0 }
    $build = if ($parsedVersion.Build -ge 0) { $parsedVersion.Build } else { 0 }

    if ($major -gt 255 -or $minor -gt 255 -or $build -gt 65535) {
        throw "Versione MSI fuori range: '$RawVersion'. Windows Installer accetta major/minor <= 255 e build <= 65535."
    }

    return "$major.$minor.$build"
}

function Reset-Directory {
    param(
        [string]$Path
    )

    $fullPath = Assert-RepositoryPathInAllowedRoot `
        -Path $Path `
        -RepositoryRoot $repoRoot `
        -AllowedRoots @($stagingRoot) `
        -Description 'Reset directory installer'

    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force -ErrorAction Stop
    }

    New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
}

function Get-RuntimeIdentifier {
    param(
        [ValidateSet('x86', 'x64')]
        [string]$MsiArchitecture
    )

    return "win-$MsiArchitecture"
}

function Copy-WinUiPublishResources {
    param(
        [string]$RuntimeIdentifier,
        [string]$PublishDir
    )

    $buildOutputDir = Join-Path $repoRoot "artifacts/bin/OnlyWinget/$Configuration/net10.0-windows10.0.17763.0/$RuntimeIdentifier"
    Assert-Path -Path $buildOutputDir -Description "WinUI build output $RuntimeIdentifier"

    $resourcePaths = @(
        'App.xbf',
        'MainWindow.xbf',
        'OnlyWinget.pri',
        'Assets',
        'Pages'
    )

    foreach ($resourcePath in $resourcePaths) {
        $sourcePath = Join-Path $buildOutputDir $resourcePath
        Assert-Path -Path $sourcePath -Description "WinUI publish resource $resourcePath"

        $destinationPath = Join-Path $PublishDir $resourcePath
        if (Test-Path -LiteralPath $destinationPath) {
            Remove-Item -LiteralPath $destinationPath -Recurse -Force -ErrorAction Stop
        }

        Copy-Item -LiteralPath $sourcePath -Destination $destinationPath -Recurse -Force -ErrorAction Stop
    }
}

function Invoke-ArchitectureMsi {
    param(
        [ValidateSet('x86', 'x64')]
        [string]$MsiArchitecture
    )

    $runtimeIdentifier = Get-RuntimeIdentifier -MsiArchitecture $MsiArchitecture
    $architectureStagingRoot = Join-Path $stagingRoot $runtimeIdentifier
    $publishDir = Join-Path $architectureStagingRoot 'publish'
    $wixObjDir = Join-Path $architectureStagingRoot 'wixobj'
    $harvestFilePath = Join-Path $architectureStagingRoot 'OnlyWinget.Harvest.wxs'
    $setupObjectPath = Join-Path $wixObjDir 'OnlyWinget.Setup.wixobj'
    $harvestObjectPath = Join-Path $wixObjDir 'OnlyWinget.Harvest.wixobj'
    $msiFilePath = Join-Path $msiOutputDir "OnlyWinget-$installerVersion-$MsiArchitecture.msi"
    $componentWin64 = if ($MsiArchitecture -eq 'x64') { 'yes' } else { 'no' }

    Reset-Directory -Path $architectureStagingRoot
    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
    New-Item -ItemType Directory -Path $wixObjDir -Force | Out-Null
    New-Item -ItemType Directory -Path $msiOutputDir -Force | Out-Null

    if (-not $NoRestore) {
        dotnet restore $projectPath --locked-mode
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet restore per il packaging $MsiArchitecture fallito."
        }
    }

    $publishArgs = @(
        'publish'
        $projectPath
        '-c'
        $Configuration
        '-f'
        'net10.0-windows10.0.17763.0'
        '-r'
        $runtimeIdentifier
        '--self-contained'
        'true'
        '--output'
        $publishDir
        '/p:UseAppHost=true'
        '/p:DebugSymbols=false'
        '/p:DebugType=None'
    )

    $publishArgs += '--no-restore'

    dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish $MsiArchitecture fallito."
    }

    $publishedExePath = Join-Path $publishDir 'OnlyWinget.exe'
    Assert-Path -Path $publishedExePath -Description "Published executable $MsiArchitecture"
    Copy-WinUiPublishResources -RuntimeIdentifier $runtimeIdentifier -PublishDir $publishDir

    & $heatExe dir $publishDir `
        -nologo `
        -cg AppFiles `
        -gg `
        -scom `
        -sreg `
        -sfrag `
        -srd `
        -dr INSTALLFOLDER `
        -var var.PublishDir `
        -out $harvestFilePath

    if ($LASTEXITCODE -ne 0) {
        throw "Harvest WiX $MsiArchitecture fallito."
    }

    [xml]$harvestXml = Get-Content -Path $harvestFilePath
    $namespaceManager = [System.Xml.XmlNamespaceManager]::new($harvestXml.NameTable)
    $namespaceManager.AddNamespace('wix', 'http://schemas.microsoft.com/wix/2006/wi')

    foreach ($componentNode in $harvestXml.SelectNodes('//wix:Component', $namespaceManager)) {
        $null = $componentNode.SetAttribute('Win64', $componentWin64)
    }

    $harvestXml.Save($harvestFilePath)

    & $candleExe `
        -nologo `
        -arch $MsiArchitecture `
        -ext $uiExtension `
        "-dPublishDir=$publishDir" `
        "-dProductVersion=$installerVersion" `
        "-dPlatform=$MsiArchitecture" `
        "-dAppIconPath=$appIconPath" `
        "-dLicenseRtfPath=$licenseRtfPath" `
        "-dInstallerDialogBmpPath=$installerDialogBmpPath" `
        "-dInstallerBannerBmpPath=$installerBannerBmpPath" `
        "-dUpgradeCode=$upgradeCode" `
        -out $wixObjDir\ `
        $wixSourcePath `
        $harvestFilePath

    if ($LASTEXITCODE -ne 0) {
        throw "Compilazione WiX $MsiArchitecture fallita."
    }

    & $lightExe `
        -nologo `
        "-sice:$($suppressedValidationIces -join ';')" `
        -ext $uiExtension `
        -out $msiFilePath `
        $setupObjectPath `
        $harvestObjectPath

    if ($LASTEXITCODE -ne 0) {
        throw "Link WiX $MsiArchitecture fallito."
    }

    $builtMsiPaths[$MsiArchitecture] = $msiFilePath
    Write-Host "MSI $MsiArchitecture generato: $msiFilePath" -ForegroundColor Green
}

function Invoke-UnifiedSetup {
    $bundleObjDir = Join-Path $stagingRoot 'bundle-wixobj'
    $bundleObjectPath = Join-Path $bundleObjDir 'OnlyWinget.Bundle.wixobj'
    $setupFilePath = Join-Path $setupOutputDir "OnlyWinget-$installerVersion-setup.exe"

    if (-not $builtMsiPaths.ContainsKey('x86') -or -not $builtMsiPaths.ContainsKey('x64')) {
        throw 'Il setup unificato richiede sia MSI x86 sia MSI x64. Usa -Architecture All.'
    }

    Reset-Directory -Path $bundleObjDir
    New-Item -ItemType Directory -Path $setupOutputDir -Force | Out-Null

    & $candleExe `
        -nologo `
        -ext $balExtension `
        -ext $utilExtension `
        "-dProductVersion=$installerVersion" `
        "-dAppIconPath=$appIconPath" `
        "-dBundleLogoPath=$bundleLogoPath" `
        "-dLicenseRtfPath=$licenseRtfPath" `
        "-dBundleThemePath=$bundleThemePath" `
        "-dBundleThemeLocalizationPath=$bundleThemeLocalizationPath" `
        "-dX86MsiPath=$($builtMsiPaths['x86'])" `
        "-dX64MsiPath=$($builtMsiPaths['x64'])" `
        "-dWindowsAppRuntimeInstallerX86Path=$resolvedWindowsAppRuntimeInstallerX86Path" `
        "-dWindowsAppRuntimeInstallerX64Path=$resolvedWindowsAppRuntimeInstallerX64Path" `
        "-dBundleUpgradeCode=$bundleUpgradeCode" `
        -out $bundleObjDir\ `
        $bundleSourcePath

    if ($LASTEXITCODE -ne 0) {
        throw 'Compilazione WiX bundle fallita.'
    }

    & $lightExe `
        -nologo `
        -ext $balExtension `
        -ext $utilExtension `
        -out $setupFilePath `
        $bundleObjectPath

    if ($LASTEXITCODE -ne 0) {
        throw 'Link WiX bundle fallito.'
    }

    Write-Host "Setup unificato generato: $setupFilePath" -ForegroundColor Green
}

Assert-Command -Name 'dotnet'
Assert-Path -Path $projectPath -Description 'Project file'
Assert-Path -Path $appIconPath -Description 'Application icon'
Assert-Path -Path $bundleLogoPath -Description 'Bundle logo'
Assert-Path -Path $licenseRtfPath -Description 'License file'
Assert-Path -Path $installerDialogBmpPath -Description 'Installer dialog bitmap'
Assert-Path -Path $installerBannerBmpPath -Description 'Installer banner bitmap'
Assert-Path -Path $wixSourcePath -Description 'WiX MSI source'
Assert-Path -Path $bundleSourcePath -Description 'WiX bundle source'
Assert-Path -Path $bundleThemePath -Description 'WiX Burn theme'
Assert-Path -Path $bundleThemeLocalizationPath -Description 'WiX Burn theme localization'

$configuredWixSearchRoots = Get-WixToolSearchRoot
$heatExe = Resolve-WixTool -ToolName 'heat.exe' -SearchRoots $configuredWixSearchRoots
$candleExe = Resolve-WixTool -ToolName 'candle.exe' -SearchRoots $configuredWixSearchRoots
$lightExe = Resolve-WixTool -ToolName 'light.exe' -SearchRoots $configuredWixSearchRoots
$wixSearchRoots = @(
    $configuredWixSearchRoots
    (Split-Path $heatExe -Parent)
    (Split-Path $candleExe -Parent)
    (Split-Path $lightExe -Parent)
)
$uiExtension = Resolve-WixExtension -ExtensionName 'WixUIExtension.dll' -SearchRoots $wixSearchRoots
$balExtension = Resolve-WixExtension -ExtensionName 'WixBalExtension.dll' -SearchRoots $wixSearchRoots
$utilExtension = Resolve-WixExtension -ExtensionName 'WixUtilExtension.dll' -SearchRoots $wixSearchRoots

$rawVersion = if ([string]::IsNullOrWhiteSpace($Version)) { Get-ProjectVersion } else { $Version }
$installerVersion = Convert-ToInstallerVersion -RawVersion $rawVersion

if (-not $SkipBundle) {
    $windowsAppSdkVersion = Get-WindowsAppSdkVersion
    $resolvedWindowsAppRuntimeInstallerX86Path = Resolve-WindowsAppRuntimeInstaller `
        -ExplicitPath $WindowsAppRuntimeInstallerPath `
        -WindowsAppSdkVersion $windowsAppSdkVersion `
        -Architecture 'x86'
    $resolvedWindowsAppRuntimeInstallerX64Path = Resolve-WindowsAppRuntimeInstaller `
        -ExplicitPath $WindowsAppRuntimeInstallerPath `
        -WindowsAppSdkVersion $windowsAppSdkVersion `
        -Architecture 'x64'
}

if ($StopRunningInstance) {
    $buildScriptPath = Join-Path $PSScriptRoot 'build.ps1'
    Assert-Path -Path $buildScriptPath -Description 'Build script'
    & $buildScriptPath -Configuration $Configuration -NoRestore:$NoRestore -StopRunningInstance
    if ($LASTEXITCODE -ne 0) {
        throw 'Preparazione build fallita prima del publish MSI.'
    }
}

$architecturesToBuild = if ($Architecture -eq 'All') { @('x86', 'x64') } else { @($Architecture) }

foreach ($architectureToBuild in $architecturesToBuild) {
    Invoke-ArchitectureMsi -MsiArchitecture $architectureToBuild
}

if (-not $SkipBundle) {
    Invoke-UnifiedSetup
}
