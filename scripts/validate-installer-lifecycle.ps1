param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$CurrentSetupPath,
    [string]$PreviousSetupPath,
    [string]$PreviousVersion,
    [switch]$NoRestore,
    [switch]$SkipPackage,
    [switch]$NonInteractive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/ScriptHelpers.ps1')

$repoRoot = Split-Path $PSScriptRoot -Parent

if (-not [string]::IsNullOrWhiteSpace($PreviousVersion)) {
    $sanitizedPrevious = $PreviousVersion.Split('-', 2)[0]
    try {
        $parsedPrevious = [Version]$sanitizedPrevious
    }
    catch {
        throw "Versione precedente non valida: '$PreviousVersion'. Usa una versione numerica, ad esempio 1.0.0."
    }

    if ($parsedPrevious.Major -ne 1 -or $parsedPrevious.Minor -ne 0) {
        throw "La versione precedente ($PreviousVersion) deve appartenere alla versione 1.0 (es. 1.0.x) come da policy."
    }
}

$packageScriptPath = Join-Path $PSScriptRoot 'package.ps1'
$artifactsPath = Join-Path $repoRoot 'artifacts'
$distPath = Join-Path $artifactsPath "dist/OnlyWinget/$Configuration"
$validationPath = Join-Path $artifactsPath "installer-validation/$Configuration"
$reportPath = Join-Path $validationPath 'installer-lifecycle-report.txt'
$productName = 'OnlyWinget'
$installFolder = Join-Path $env:ProgramFiles $productName

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'La validazione installer richiede PowerShell elevato come amministratore.'
    }
}

function Get-InstalledOnlyWingetProduct {
    param(
        [switch]$VisibleOnly
    )

    $roots = @(
        'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall',
        'HKLM:\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall'
    )

    foreach ($root in $roots) {
        if (-not (Test-Path -LiteralPath $root)) {
            continue
        }

        Get-ChildItem -LiteralPath $root | ForEach-Object {
            $item = Get-ItemProperty -LiteralPath $_.PSPath -ErrorAction SilentlyContinue
            $propDisplayName = $item.PSObject.Properties['DisplayName']
            if ($null -ne $propDisplayName -and $propDisplayName.Value -eq $productName) {
                $propSystemComponent = $item.PSObject.Properties['SystemComponent']
                $systemComponent = if ($null -ne $propSystemComponent) { $propSystemComponent.Value } else { $null }
                if ($VisibleOnly -and $systemComponent -eq 1) {
                    return
                }

                $propDisplayVersion = $item.PSObject.Properties['DisplayVersion']
                $propInstallLocation = $item.PSObject.Properties['InstallLocation']
                $propUninstallString = $item.PSObject.Properties['UninstallString']

                [pscustomobject]@{
                    Key = $_.PSChildName
                    DisplayVersion = if ($null -ne $propDisplayVersion) { $propDisplayVersion.Value } else { $null }
                    InstallLocation = if ($null -ne $propInstallLocation) { $propInstallLocation.Value } else { $null }
                    SystemComponent = $systemComponent
                    UninstallString = if ($null -ne $propUninstallString) { $propUninstallString.Value } else { $null }
                }
            }
        }
    }
}

function Assert-CleanInstallState {
    $installed = @(Get-InstalledOnlyWingetProduct)
    if ($installed.Count -gt 0 -or (Test-Path -LiteralPath $installFolder)) {
        throw "OnlyWinget risulta gia' installato. Usa un host pulito o disinstalla manualmente prima della validazione."
    }
}

function Get-ProjectVersion {
    [xml]$projectXml = Get-Content -LiteralPath (Join-Path $repoRoot 'src/OnlyWinget/OnlyWinget.csproj')
    $version = $projectXml.Project.PropertyGroup |
        Where-Object { $_.Version } |
        Select-Object -ExpandProperty Version -First 1

    if ([string]::IsNullOrWhiteSpace($version)) {
        throw 'Versione progetto non trovata.'
    }

    $trimmed = $version.Trim()
    $sanitized = $trimmed.Split('-', 2)[0]
    try {
        $parsedVersion = [Version]$sanitized
    }
    catch {
        throw "Versione progetto non valida: '$trimmed'."
    }

    if ($parsedVersion.Major -ne 1 -or $parsedVersion.Minor -ne 0) {
        throw "La versione del progetto deve rimanere alla versione 1.0 (es. 1.0.x) come da policy. Versione rilevata: '$trimmed'."
    }

    return $trimmed
}

function Get-DefaultPreviousVersion {
    $current = [Version](Get-ProjectVersion)
    if ($current.Build -le 0) {
        throw 'Impossibile calcolare una versione precedente automatica. Passa -PreviousVersion o -PreviousSetupPath.'
    }

    return "$($current.Major).$($current.Minor).$($current.Build - 1)"
}

function Invoke-NsisInstall {
    param(
        [string]$SetupPath
    )

    Assert-Path -Path $SetupPath -Description 'NSIS Setup EXE'
    $process = Start-Process -FilePath $SetupPath -ArgumentList '/S' -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0) {
        throw "NSIS Setup fallito con exit code $($process.ExitCode)."
    }

    return "NSIS silent execution OK"
}

function Invoke-NsisUninstall {
    $uninstallerPath = Join-Path $installFolder 'Uninstall.exe'
    Assert-Path -Path $uninstallerPath -Description 'NSIS Uninstall EXE'
    $process = Start-Process -FilePath $uninstallerPath -ArgumentList '/S' -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0) {
        throw "NSIS Uninstall fallito con exit code $($process.ExitCode)."
    }

    return "NSIS silent uninstall OK"
}

function Resolve-LatestSetup {
    $setup = Get-ChildItem -LiteralPath $distPath -Filter '*-setup.exe' |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if ($null -eq $setup) {
        throw "Setup corrente non trovato in '$distPath'."
    }

    return $setup.FullName
}

function New-SetupArtifact {
    param(
        [string]$Version
    )

    $packageParameters = @{
        Configuration = $Configuration
        NoRestore = $NoRestore
    }

    if (-not [string]::IsNullOrWhiteSpace($Version)) {
        $packageParameters.Version = $Version
    }

    $packageOutput = & $packageScriptPath @packageParameters

    foreach ($line in $packageOutput) {
        Write-Host $line
    }

    if ($LASTEXITCODE -ne 0) {
        throw 'Packaging setup fallito.'
    }

    return Resolve-LatestSetup
}

function Assert-SingleInstalledProduct {
    param(
        [string]$ExpectedVersion
    )

    $installed = @(Get-InstalledOnlyWingetProduct -VisibleOnly)
    if ($installed.Count -ne 1) {
        throw "Atteso 1 prodotto installato visibile, trovati $($installed.Count)."
    }

    if ($installed[0].SystemComponent -eq 1) {
        throw 'OnlyWinget e'' installato ma nascosto da Programmi e funzionalita.'
    }

    if (-not [string]::IsNullOrWhiteSpace($ExpectedVersion) -and $installed[0].DisplayVersion -ne $ExpectedVersion) {
        throw "Versione installata inattesa. Attesa $ExpectedVersion, trovata $($installed[0].DisplayVersion)."
    }

    Assert-Path -Path (Join-Path $installFolder 'OnlyWinget.exe') -Description 'Installed executable'
}

function Assert-StartMenuShortcut {
    $shortcutPaths = @(
        (Join-Path ([Environment]::GetFolderPath('CommonPrograms')) 'OnlyWinget/OnlyWinget.lnk'),
        (Join-Path ([Environment]::GetFolderPath('Programs')) 'OnlyWinget/OnlyWinget.lnk')
    )

    if (-not ($shortcutPaths | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1)) {
        throw "Shortcut Start Menu non trovato. Percorsi controllati: $($shortcutPaths -join '; ')"
    }
}

function Assert-DesktopShortcut {
    $shortcutPaths = @(
        (Join-Path ([Environment]::GetFolderPath('CommonDesktopDirectory')) 'OnlyWinget.lnk'),
        (Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'OnlyWinget.lnk')
    )

    if (-not ($shortcutPaths | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1)) {
        throw "Shortcut Desktop non trovato. Percorsi controllati: $($shortcutPaths -join '; ')"
    }
}

function Assert-AppLaunch {
    $exePath = Join-Path $installFolder 'OnlyWinget.exe'
    $process = Start-Process -FilePath $exePath -PassThru -WindowStyle Minimized
    Start-Sleep -Seconds 3

    if ($process.HasExited -and $process.ExitCode -ne 0) {
        throw "OnlyWinget si e' chiuso con exit code $($process.ExitCode) dopo il lancio."
    }

    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
    }
}

Assert-Command -Name 'dotnet'
Assert-Path -Path $packageScriptPath -Description 'Packaging script'
Assert-Administrator
New-Item -ItemType Directory -Path $validationPath -Force | Out-Null
Assert-CleanInstallState

if (-not $SkipPackage -and [string]::IsNullOrWhiteSpace($CurrentSetupPath)) {
    if ([string]::IsNullOrWhiteSpace($PreviousSetupPath)) {
        $effectivePreviousVersion = if ([string]::IsNullOrWhiteSpace($PreviousVersion)) {
            Get-DefaultPreviousVersion
        }
        else {
            $PreviousVersion
        }

        $PreviousSetupPath = New-SetupArtifact -Version $effectivePreviousVersion
    }

    $CurrentSetupPath = New-SetupArtifact -Version ''
}
elseif ([string]::IsNullOrWhiteSpace($CurrentSetupPath)) {
    $CurrentSetupPath = Resolve-LatestSetup
}

$currentVersion = Get-ProjectVersion
$reportLines = [System.Collections.Generic.List[string]]::new()
$reportLines.Add("ValidationStartedAt: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$reportLines.Add("CurrentSetup: $CurrentSetupPath")
$reportLines.Add("PreviousSetup: $PreviousSetupPath")

$localAppDataPath = Join-Path $env:LOCALAPPDATA $productName
$sentinelPath = Join-Path $localAppDataPath 'installer-validation-sentinel.txt'
$createdLocalDataDirectory = -not (Test-Path -LiteralPath $localAppDataPath)
New-Item -ItemType Directory -Path $localAppDataPath -Force | Out-Null
Set-Content -LiteralPath $sentinelPath -Value 'installer validation sentinel' -Encoding UTF8

$wasInstalled = $false
try {
    if (-not [string]::IsNullOrWhiteSpace($PreviousSetupPath)) {
        $reportLines.Add("PreviousInstallLog: $(Invoke-NsisInstall -SetupPath $PreviousSetupPath)")
        $wasInstalled = $true
        Assert-SingleInstalledProduct -ExpectedVersion ''
        $reportLines.Add('PreviousInstall: OK')
    }

    $reportLines.Add("CurrentInstallOrUpgradeLog: $(Invoke-NsisInstall -SetupPath $CurrentSetupPath)")
    $wasInstalled = $true
    Assert-SingleInstalledProduct -ExpectedVersion $currentVersion
    Assert-StartMenuShortcut
    Assert-DesktopShortcut
    Assert-AppLaunch
    $reportLines.Add('CurrentInstallOrUpgrade: OK')
    $reportLines.Add('LaunchAfterInstallOrUpgrade: OK')
}
finally {
    if ($wasInstalled -and (Test-Path -LiteralPath (Join-Path $installFolder 'Uninstall.exe'))) {
        $reportLines.Add("UninstallLog: $(Invoke-NsisUninstall)")
    }
}

$installedAfterUninstall = @(Get-InstalledOnlyWingetProduct -VisibleOnly)
if ($installedAfterUninstall.Count -gt 0) {
    throw "Disinstallazione incompleta: prodotti residui $($installedAfterUninstall.Count)."
}

if (Test-Path -LiteralPath $installFolder) {
    throw "La cartella applicazione non e' stata rimossa: $installFolder"
}

Assert-Path -Path $sentinelPath -Description 'LocalAppData sentinel'
Remove-Item -LiteralPath $sentinelPath -Force
if ($createdLocalDataDirectory) {
    Remove-Item -LiteralPath $localAppDataPath -Force -ErrorAction SilentlyContinue
}

$reportLines.Add('Uninstall: OK')
$reportLines.Add('LocalAppDataPreserved: OK')
$reportLines.Add("ValidationCompletedAt: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$reportLines | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host "Validazione installer NSIS completata: $reportPath" -ForegroundColor Green
