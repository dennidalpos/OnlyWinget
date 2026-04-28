param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$CurrentSetupPath,
    [string]$PreviousSetupPath,
    [string]$PreviousVersion,
    [switch]$NoRestore,
    [switch]$SkipPackage
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'internal/ScriptHelpers.ps1')

$repoRoot = Split-Path $PSScriptRoot -Parent
$packageScriptPath = Join-Path $PSScriptRoot 'package.ps1'
$artifactsPath = Join-Path $repoRoot 'artifacts'
$distPath = Join-Path $artifactsPath "dist/OnlyWinget/$Configuration"
$validationPath = Join-Path $artifactsPath "installer-validation/$Configuration"
$reportPath = Join-Path $validationPath 'installer-lifecycle-report.txt'
$productName = 'OnlyWinget'
$installFolder = if ([Environment]::Is64BitOperatingSystem) {
    Join-Path $env:ProgramFiles $productName
}
else {
    Join-Path ${env:ProgramFiles(x86)} $productName
}

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'La validazione installer richiede PowerShell elevato come amministratore.'
    }
}

function Get-InstalledOnlyWingetProducts {
    param(
        [switch]$VisibleOnly
    )

    $roots = @(
        'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall',
        'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall'
    )

    foreach ($root in $roots) {
        if (-not (Test-Path -LiteralPath $root)) {
            continue
        }

        Get-ChildItem -LiteralPath $root | ForEach-Object {
            $item = Get-ItemProperty -LiteralPath $_.PSPath -ErrorAction SilentlyContinue
            $displayName = $item.PSObject.Properties['DisplayName']?.Value
            if ($displayName -eq $productName) {
                $systemComponent = $item.PSObject.Properties['SystemComponent']?.Value
                if ($VisibleOnly -and $systemComponent -eq 1) {
                    return
                }

                [pscustomobject]@{
                    Key = $_.PSChildName
                    DisplayVersion = $item.PSObject.Properties['DisplayVersion']?.Value
                    InstallLocation = $item.PSObject.Properties['InstallLocation']?.Value
                    SystemComponent = $systemComponent
                    UninstallString = $item.PSObject.Properties['UninstallString']?.Value
                }
            }
        }
    }
}

function Assert-CleanInstallState {
    $installed = @(Get-InstalledOnlyWingetProducts)
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

    return $version.Trim()
}

function Get-DefaultPreviousVersion {
    $current = [Version](Get-ProjectVersion)
    if ($current.Build -le 0) {
        throw 'Impossibile calcolare una versione precedente automatica. Passa -PreviousVersion o -PreviousSetupPath.'
    }

    return "$($current.Major).$($current.Minor).$($current.Build - 1)"
}

function Invoke-Setup {
    param(
        [string]$SetupPath,
        [string[]]$Arguments,
        [string]$LogName
    )

    Assert-Path -Path $SetupPath -Description 'Setup EXE'
    $logPath = Join-Path $validationPath $LogName
    $setupArguments = @($Arguments + @('/norestart', '/log', $logPath))
    $process = Start-Process -FilePath $SetupPath -ArgumentList $setupArguments -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0 -and $process.ExitCode -ne 3010) {
        throw "Setup fallito con exit code $($process.ExitCode). Log: $logPath"
    }

    return $logPath
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

    if ([string]::IsNullOrWhiteSpace($Version)) {
        $packageOutput = & $packageScriptPath -Configuration $Configuration -NoRestore:$NoRestore
    }
    else {
        $packageOutput = & $packageScriptPath -Configuration $Configuration -NoRestore:$NoRestore -Version $Version
    }

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

    $installed = @(Get-InstalledOnlyWingetProducts -VisibleOnly)
    if ($installed.Count -ne 1) {
        throw "Atteso 1 prodotto installato visibile, trovati $($installed.Count)."
    }

    $internalProducts = @(Get-InstalledOnlyWingetProducts | Where-Object { $_.SystemComponent -eq 1 })
    if ($internalProducts.Count -gt 1) {
        throw "Atteso al massimo 1 MSI interno nascosto, trovati $($internalProducts.Count)."
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

function Assert-DesktopShortcutDefault {
    $shortcutPaths = @(
        (Join-Path ([Environment]::GetFolderPath('CommonDesktopDirectory')) 'OnlyWinget.lnk'),
        (Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'OnlyWinget.lnk')
    )

    if ($shortcutPaths | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1) {
        throw 'Il collegamento Desktop non deve essere installato dal setup quiet predefinito.'
    }
}

function Assert-AppLaunches {
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

try {
    $installedSetupPath = $null
    if (-not [string]::IsNullOrWhiteSpace($PreviousSetupPath)) {
        $reportLines.Add("PreviousInstallLog: $(Invoke-Setup -SetupPath $PreviousSetupPath -Arguments @('/quiet') -LogName 'previous-install.log')")
        $installedSetupPath = $PreviousSetupPath
        Assert-SingleInstalledProduct -ExpectedVersion ''
        $reportLines.Add('PreviousInstall: OK')
    }

    $reportLines.Add("CurrentInstallOrUpgradeLog: $(Invoke-Setup -SetupPath $CurrentSetupPath -Arguments @('/quiet') -LogName 'current-install-or-upgrade.log')")
    $installedSetupPath = $CurrentSetupPath
    Assert-SingleInstalledProduct -ExpectedVersion $currentVersion
    Assert-StartMenuShortcut
    Assert-DesktopShortcutDefault
    Assert-AppLaunches
    $reportLines.Add('CurrentInstallOrUpgrade: OK')
    $reportLines.Add('LaunchAfterInstallOrUpgrade: OK')
    $reportLines.Add("RepairLog: $(Invoke-Setup -SetupPath $CurrentSetupPath -Arguments @('/repair', '/quiet') -LogName 'repair.log')")
    Assert-SingleInstalledProduct -ExpectedVersion $currentVersion
    $reportLines.Add('Repair: OK')
}
finally {
    if ($null -ne $installedSetupPath) {
        $reportLines.Add("UninstallLog: $(Invoke-Setup -SetupPath $installedSetupPath -Arguments @('/uninstall', '/quiet') -LogName 'uninstall.log')")
    }
}

$installedAfterUninstall = @(Get-InstalledOnlyWingetProducts -VisibleOnly)
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

Write-Host "Validazione installer completata: $reportPath" -ForegroundColor Green
