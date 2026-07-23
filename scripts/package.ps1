param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$Version,
    [switch]$NoRestore,
    [switch]$StopRunningInstance,
    [switch]$NonInteractive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/ScriptHelpers.ps1')

$repoRoot = Split-Path $PSScriptRoot -Parent
$projectPath = Join-Path $repoRoot 'src/OnlyWinget/OnlyWinget.csproj'
$artifactsPath = Join-Path $repoRoot 'artifacts'
$stagingRoot = Join-Path $artifactsPath 'installer'
$setupOutputDir = Join-Path $artifactsPath "dist/OnlyWinget/$Configuration"

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
        throw "Versione non valida: '$RawVersion'. Usa una versione numerica compatibile, ad esempio 1.0.0."
    }

    $major = $parsedVersion.Major
    $minor = if ($parsedVersion.Minor -ge 0) { $parsedVersion.Minor } else { 0 }
    $build = if ($parsedVersion.Build -ge 0) { $parsedVersion.Build } else { 0 }

    if ($major -ne 1 -or $minor -ne 0) {
        throw "La versione dell'applicazione deve essere bloccata a 1.0 (es. 1.0.x) come da policy. Versione rilevata: '$RawVersion'."
    }

    if ($major -gt 255 -or $minor -gt 255 -or $build -gt 65535) {
        throw "Versione fuori range: '$RawVersion'."
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

function Copy-WinUiPublishResource {
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
        'Controls',
        'DesignSystem',
        'Features'
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

function Resolve-MakensisExe {
    $candidates = @(
        'C:\Program Files (x86)\NSIS\makensis.exe',
        'C:\Program Files\NSIS\makensis.exe'
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    $cmd = Get-Command 'makensis' -ErrorAction SilentlyContinue
    if ($null -ne $cmd) {
        return $cmd.Source
    }

    throw "Tool NSIS non trovato (makensis.exe). Assicurati che NSIS sia installato in 'C:\Program Files (x86)\NSIS' o presente nel PATH."
}

function Invoke-PublishAndPackage {
    $runtimeIdentifier = 'win-x64'
    $nsisStagingRoot = Join-Path $stagingRoot $runtimeIdentifier
    $publishDir = Join-Path $nsisStagingRoot 'publish'
    $nsisScriptPath = Join-Path $repoRoot 'src/OnlyWinget.Setup/OnlyWinget.nsi'
    $setupFilePath = Join-Path $setupOutputDir "OnlyWinget-$installerVersion-setup.exe"
    $portableFilePath = Join-Path $setupOutputDir "OnlyWinget-$installerVersion-portable-x64.zip"

    Assert-Path -Path $nsisScriptPath -Description 'NSIS setup script'
    Reset-Directory -Path $nsisStagingRoot
    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
    New-Item -ItemType Directory -Path $setupOutputDir -Force | Out-Null

    if (-not $NoRestore) {
        dotnet restore $projectPath -r $runtimeIdentifier --locked-mode --no-dependencies
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet restore per il packaging $runtimeIdentifier fallito."
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
        '/p:WindowsAppSDKSelfContained=true'
        '/p:BuildProjectReferences=false'
        '/p:DebugSymbols=false'
        '/p:DebugType=None'
    )

    if ($NoRestore) {
        $publishArgs += '--no-restore'
    }

    dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet publish x64 fallito.'
    }

    $publishedExePath = Join-Path $publishDir 'OnlyWinget.exe'
    Assert-Path -Path $publishedExePath -Description 'Published executable x64'
    Copy-WinUiPublishResource -RuntimeIdentifier $runtimeIdentifier -PublishDir $publishDir

    # 1. NSIS Installer Setup EXE
    $makensisExe = Resolve-MakensisExe
    $nsisArgs = @(
        "-DPRODUCT_VERSION=$installerVersion",
        "-DPUBLISH_DIR=$publishDir",
        "-DOUT_FILE=$setupFilePath",
        $nsisScriptPath
    )

    & $makensisExe @nsisArgs
    if ($LASTEXITCODE -ne 0) {
        throw 'Compilazione NSIS setup fallita.'
    }
    Assert-Path -Path $setupFilePath -Description 'NSIS setup executable'
    Write-Host "Setup NSIS generato: $setupFilePath" -ForegroundColor Green

    # 2. Portable ZIP
    if (Test-Path -LiteralPath $portableFilePath) {
        Remove-Item -LiteralPath $portableFilePath -Force -ErrorAction Stop
    }
    Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $portableFilePath -CompressionLevel Optimal
    Assert-Path -Path $portableFilePath -Description 'Portable x64 archive'
    Write-Host "Portable x64 generata: $portableFilePath" -ForegroundColor Green
}

Assert-Command -Name 'dotnet'
Assert-Path -Path $projectPath -Description 'Project file'

$rawVersion = if ([string]::IsNullOrWhiteSpace($Version)) { Get-ProjectVersion } else { $Version }
$installerVersion = Convert-ToInstallerVersion -RawVersion $rawVersion

$buildScriptPath = Join-Path $PSScriptRoot 'build.ps1'
Assert-Path -Path $buildScriptPath -Description 'Build script'

New-Item -ItemType Directory -Path $artifactsPath -Force | Out-Null
$packageLockPath = Join-Path $artifactsPath '.package.lock'
try {
    $packageLock = [System.IO.File]::Open(
        $packageLockPath,
        [System.IO.FileMode]::OpenOrCreate,
        [System.IO.FileAccess]::ReadWrite,
        [System.IO.FileShare]::None)
}
catch [System.IO.IOException] {
    throw "Packaging gia' in esecuzione per questo repository. Attendi il completamento dell'altro processo e riprova. Lock: $packageLockPath"
}

try {
    & $buildScriptPath -Configuration $Configuration -NoRestore:$NoRestore -StopRunningInstance:$StopRunningInstance -NonInteractive:$NonInteractive
    if ($LASTEXITCODE -ne 0) {
        throw 'Preparazione build fallita prima del packaging.'
    }

    Invoke-PublishAndPackage
}
finally {
    $packageLock.Dispose()
    Remove-Item -LiteralPath $packageLockPath -Force -ErrorAction SilentlyContinue
}
