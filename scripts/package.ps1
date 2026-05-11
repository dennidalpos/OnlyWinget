param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$Version,
    [switch]$NoRestore,
    [switch]$StopRunningInstance,
    [ValidateSet('x86', 'x64', 'All')]
    [string]$Architecture = 'All',
    [switch]$SkipBundle
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'internal/ScriptHelpers.ps1')

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

function Resolve-WixTool {
    param(
        [string]$ToolName
    )

    $localCandidate = Join-Path $repoRoot "tools/wix314-binaries/$ToolName"
    if (Test-Path $localCandidate) {
        return $localCandidate
    }

    $command = Get-Command $ToolName -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    throw "Tool WiX non trovato: $ToolName. Installa WiX Toolset 3.x o aggiungi i binari in 'tools/wix314-binaries'."
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

    if (Test-Path $Path) {
        Remove-Item -Path $Path -Recurse -Force
    }

    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Get-RuntimeIdentifier {
    param(
        [ValidateSet('x86', 'x64')]
        [string]$MsiArchitecture
    )

    return "win-$MsiArchitecture"
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
        'net8.0-windows'
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
        -ext $utilExtension `
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
        -ext $utilExtension `
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
        "-dProductVersion=$installerVersion" `
        "-dAppIconPath=$appIconPath" `
        "-dBundleLogoPath=$bundleLogoPath" `
        "-dLicenseRtfPath=$licenseRtfPath" `
        "-dBundleThemePath=$bundleThemePath" `
        "-dBundleThemeLocalizationPath=$bundleThemeLocalizationPath" `
        "-dX86MsiPath=$($builtMsiPaths['x86'])" `
        "-dX64MsiPath=$($builtMsiPaths['x64'])" `
        "-dBundleUpgradeCode=$bundleUpgradeCode" `
        -out $bundleObjDir\ `
        $bundleSourcePath

    if ($LASTEXITCODE -ne 0) {
        throw 'Compilazione WiX bundle fallita.'
    }

    & $lightExe `
        -nologo `
        -ext $balExtension `
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

$heatExe = Resolve-WixTool -ToolName 'heat.exe'
$candleExe = Resolve-WixTool -ToolName 'candle.exe'
$lightExe = Resolve-WixTool -ToolName 'light.exe'
$wixSearchRoots = @(
    (Join-Path $repoRoot 'tools/wix314-binaries')
    (Split-Path $heatExe -Parent)
    (Split-Path $candleExe -Parent)
    (Split-Path $lightExe -Parent)
)
$utilExtension = Resolve-WixExtension -ExtensionName 'WixUtilExtension.dll' -SearchRoots $wixSearchRoots
$uiExtension = Resolve-WixExtension -ExtensionName 'WixUIExtension.dll' -SearchRoots $wixSearchRoots
$balExtension = Resolve-WixExtension -ExtensionName 'WixBalExtension.dll' -SearchRoots $wixSearchRoots

$rawVersion = if ([string]::IsNullOrWhiteSpace($Version)) { Get-ProjectVersion } else { $Version }
$installerVersion = Convert-ToInstallerVersion -RawVersion $rawVersion

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
