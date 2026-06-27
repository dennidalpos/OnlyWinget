$script:OnlyWingetScriptsRoot = Split-Path $PSScriptRoot -Parent
$script:OnlyWingetRepositoryRoot = Split-Path $script:OnlyWingetScriptsRoot -Parent
$script:OnlyWingetApprovedWindowsAppRuntimeRedistDownloads = @{}

function Test-OnlyWingetInteractiveShell {
    if ([Console]::IsInputRedirected) {
        return $false
    }

    return $null -ne $Host -and
        $null -ne $Host.UI -and
        $null -ne $Host.UI.RawUI
}

function Read-OnlyWingetYesNo {
    param(
        [string]$Prompt,
        [bool]$DefaultYes = $true
    )

    if (-not (Test-OnlyWingetInteractiveShell)) {
        return $DefaultYes
    }

    $suffix = if ($DefaultYes) { '[S/n]' } else { '[s/N]' }
    $answer = Read-Host "$Prompt $suffix"
    if ([string]::IsNullOrWhiteSpace($answer)) {
        return $DefaultYes
    }

    return $answer.Trim().StartsWith('s', [System.StringComparison]::OrdinalIgnoreCase) -or
        $answer.Trim().StartsWith('y', [System.StringComparison]::OrdinalIgnoreCase)
}

function Test-OnlyWingetAutoInstallAllowed {
    param(
        [string]$Description
    )

    if ($env:ONLYWINGET_SKIP_AUTO_INSTALL -eq '1') {
        throw "$Description mancante. Installazione automatica disabilitata da ONLYWINGET_SKIP_AUTO_INSTALL=1."
    }

    if (Test-OnlyWingetInteractiveShell) {
        return Read-OnlyWingetYesNo -Prompt "$Description mancante. Installarlo ora?"
    }

    return $true
}

function Invoke-OnlyWingetExternalInstall {
    param(
        [string]$Command,
        [string[]]$Arguments,
        [string]$Description
    )

    Write-Host "Installazione prerequisito: $Description" -ForegroundColor Cyan
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Installazione fallita: $Description"
    }
}

function Get-RequiredDotNetSdkMajor {
    $globalJsonPath = Join-Path $script:OnlyWingetRepositoryRoot 'global.json'
    if (-not (Test-Path -LiteralPath $globalJsonPath)) {
        return '10'
    }

    $globalJson = Get-Content -LiteralPath $globalJsonPath -Raw | ConvertFrom-Json
    $version = [string]$globalJson.sdk.version
    if ([string]::IsNullOrWhiteSpace($version)) {
        return '10'
    }

    return $version.Split('.')[0]
}

function Install-DotNetSdk {
    $major = Get-RequiredDotNetSdkMajor
    $winget = Get-Command 'winget' -ErrorAction SilentlyContinue
    if ($null -eq $winget) {
        throw ".NET SDK $major richiesto, ma winget non e' disponibile per installarlo automaticamente."
    }

    Invoke-OnlyWingetExternalInstall `
        -Command $winget.Source `
        -Arguments @(
            'install',
            '--id',
            "Microsoft.DotNet.SDK.$major",
            '--exact',
            '--source',
            'winget',
            '--accept-source-agreements',
            '--accept-package-agreements',
            '--silent'
        ) `
        -Description ".NET SDK $major"
}

function Install-WixToolset {
    if (-not (Test-OnlyWingetAutoInstallAllowed -Description 'WiX Toolset 3.x')) {
        throw 'WiX Toolset 3.x richiesto per il packaging.'
    }

    $choco = Get-Command 'choco' -ErrorAction SilentlyContinue
    if ($null -ne $choco) {
        Invoke-OnlyWingetExternalInstall `
            -Command $choco.Source `
            -Arguments @('install', 'wixtoolset', '--version=3.14.1.20250415', '--no-progress', '-y') `
            -Description 'WiX Toolset 3.14'
        return
    }

    $winget = Get-Command 'winget' -ErrorAction SilentlyContinue
    if ($null -ne $winget) {
        Invoke-OnlyWingetExternalInstall `
            -Command $winget.Source `
            -Arguments @(
                'install',
                '--id',
                'WiXToolset.WiXToolset',
                '--exact',
                '--source',
                'winget',
                '--accept-source-agreements',
                '--accept-package-agreements',
                '--silent'
            ) `
            -Description 'WiX Toolset 3.x'
        return
    }

    throw 'WiX Toolset 3.x non trovato e nessun installer automatico disponibile (winget/choco).'
}

function Install-PowerShellModuleIfMissing {
    param(
        [string]$Name
    )

    $module = Get-Module -ListAvailable -Name $Name |
        Sort-Object Version -Descending |
        Select-Object -First 1
    if ($null -ne $module) {
        return $module
    }

    if (-not (Test-OnlyWingetAutoInstallAllowed -Description "Modulo PowerShell $Name")) {
        throw "Modulo PowerShell richiesto non installato: $Name"
    }

    Install-Module -Name $Name -Scope CurrentUser -Repository PSGallery -Force -AllowClobber

    $module = Get-Module -ListAvailable -Name $Name |
        Sort-Object Version -Descending |
        Select-Object -First 1
    if ($null -eq $module) {
        throw "Modulo PowerShell $Name non trovato dopo l'installazione."
    }

    return $module
}

function Enter-InteractiveModeIfNoParameter {
    param(
        [hashtable]$BoundParameters,
        [string]$ScriptRoot,
        [switch]$NonInteractive
    )

    if ($NonInteractive) {
        return $false
    }

    $effectiveParameterNames = @($BoundParameters.Keys | Where-Object { $_ -ne 'NonInteractive' })
    if ($effectiveParameterNames.Count -gt 0) {
        return $false
    }

    if (-not (Test-OnlyWingetInteractiveShell)) {
        throw 'Script avviato senza parametri in una sessione non interattiva. Passa parametri espliciti oppure usa scripts/run.ps1 -Task <nome> -NonInteractive.'
    }

    & (Join-Path $ScriptRoot 'run.ps1')
    return $true
}

function Install-WindowsAppRuntimeRedist {
    param(
        [string]$WindowsAppSdkVersion
    )

    $architecture = 'x64'

    if ($env:ONLYWINGET_SKIP_AUTO_INSTALL -eq '1') {
        throw "Windows App Runtime redist $WindowsAppSdkVersion mancante. Download automatico disabilitato da ONLYWINGET_SKIP_AUTO_INSTALL=1."
    }

    $versionParts = $WindowsAppSdkVersion.Split('.')
    if ($versionParts.Count -lt 2) {
        throw "Versione Microsoft.WindowsAppSDK non valida: $WindowsAppSdkVersion"
    }

    $majorMinor = "$($versionParts[0]).$($versionParts[1])"
    $dependencyRoot = Join-Path $script:OnlyWingetRepositoryRoot "dependencies/windowsappsdk/$WindowsAppSdkVersion"
    $extractRoot = Join-Path $dependencyRoot 'redist'
    $zipPath = Join-Path $dependencyRoot "Microsoft.WindowsAppRuntime.Redist.$WindowsAppSdkVersion.zip"

    New-Item -ItemType Directory -Path $dependencyRoot -Force | Out-Null
    Write-Host "Windows App Runtime redist $WindowsAppSdkVersion ($architecture): cache $dependencyRoot" -ForegroundColor Cyan

    if (Test-Path -LiteralPath $extractRoot) {
        $existingInstaller = Get-ChildItem -Path $extractRoot -Recurse -Filter "WindowsAppRuntimeInstall-$architecture.exe" -File -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($null -ne $existingInstaller) {
            Write-Host "Uso redist in cache: $($existingInstaller.FullName)" -ForegroundColor Green
            return $existingInstaller.FullName
        }
    }

    $downloadUrls = @(
        "https://aka.ms/windowsappsdk/$majorMinor/$WindowsAppSdkVersion/Microsoft.WindowsAppRuntime.Redist.$majorMinor.zip",
        "https://aka.ms/windowsappsdk/$majorMinor/$WindowsAppSdkVersion/Microsoft.WindowsAppRuntime.Redist.$WindowsAppSdkVersion.zip"
    )

    if (-not (Test-Path -LiteralPath $zipPath)) {
        if ((Test-OnlyWingetInteractiveShell) -and
            -not $script:OnlyWingetApprovedWindowsAppRuntimeRedistDownloads.ContainsKey($WindowsAppSdkVersion) -and
            -not (Read-OnlyWingetYesNo -Prompt "File redist Windows App Runtime $WindowsAppSdkVersion mancante per il bundle. Scaricarlo ora in '$dependencyRoot'?")) {
            throw "Windows App Runtime installer richiesto per includere Microsoft.WindowsAppSDK $WindowsAppSdkVersion nel bundle."
        }

        $script:OnlyWingetApprovedWindowsAppRuntimeRedistDownloads[$WindowsAppSdkVersion] = $true
        $downloaded = $false
        foreach ($downloadUrl in $downloadUrls) {
            try {
                Write-Host "Download Windows App Runtime redist: $downloadUrl" -ForegroundColor Cyan
                Write-Host "Destinazione: $zipPath" -ForegroundColor Cyan
                Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath -UseBasicParsing -ErrorAction Stop
                $downloaded = $true
                break
            }
            catch {
                Write-Warning "Download non riuscito: $downloadUrl"
            }
        }

        if (-not $downloaded) {
            throw "Impossibile scaricare Windows App Runtime redist $WindowsAppSdkVersion."
        }
    }
    else {
        Write-Host "Uso archivio redist in cache: $zipPath" -ForegroundColor Green
    }

    if (Test-Path -LiteralPath $extractRoot) {
        Write-Host "Aggiorno estrazione redist: $extractRoot" -ForegroundColor Cyan
        Remove-Item -LiteralPath $extractRoot -Recurse -Force -ErrorAction Stop
    }

    Write-Host "Estrazione Windows App Runtime redist: $extractRoot" -ForegroundColor Cyan
    Expand-Archive -LiteralPath $zipPath -DestinationPath $extractRoot -Force
    $installer = Get-ChildItem -Path $extractRoot -Recurse -Filter "WindowsAppRuntimeInstall-$architecture.exe" -File |
        Select-Object -First 1
    if ($null -eq $installer) {
        $installer = Get-ChildItem -Path $extractRoot -Recurse -Filter 'WindowsAppRuntimeInstall.exe' -File |
            Select-Object -First 1
    }

    if ($null -eq $installer) {
        throw "WindowsAppRuntimeInstall-$architecture.exe non trovato nel redist $WindowsAppSdkVersion."
    }

    Write-Host "Redist risolto ($architecture): $($installer.FullName)" -ForegroundColor Green
    return $installer.FullName
}

function Assert-Command {
    param(
        [string]$Name
    )

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        if ($Name -eq 'dotnet' -and (Test-OnlyWingetAutoInstallAllowed -Description '.NET SDK')) {
            Install-DotNetSdk
        }
    }

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Comando richiesto non trovato: $Name"
    }
}

function Assert-Path {
    param(
        [string]$Path,
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Description non trovato: $Path"
    }
}

function Get-NormalizedFullPath {
    param(
        [string]$Path
    )

    return [System.IO.Path]::GetFullPath($Path).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
}

function Test-IsSameOrChildPath {
    param(
        [string]$Path,
        [string]$ParentPath
    )

    $fullPath = Get-NormalizedFullPath -Path $Path
    $fullParentPath = Get-NormalizedFullPath -Path $ParentPath

    return $fullPath.Equals($fullParentPath, [System.StringComparison]::OrdinalIgnoreCase) -or
        $fullPath.StartsWith($fullParentPath + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)
}

function Assert-RepositoryPathInAllowedRoot {
    param(
        [string]$Path,
        [string]$RepositoryRoot,
        [string[]]$AllowedRoots,
        [string]$Description = 'Percorso'
    )

    $fullPath = Get-NormalizedFullPath -Path $Path
    $fullRepositoryRoot = Get-NormalizedFullPath -Path $RepositoryRoot

    if ($fullPath.Equals($fullRepositoryRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-IsSameOrChildPath -Path $fullPath -ParentPath $fullRepositoryRoot)) {
        throw "$Description rifiutato perche' fuori repository o coincidente con la radice repository: $Path"
    }

    foreach ($allowedRoot in $AllowedRoots) {
        if (Test-IsSameOrChildPath -Path $fullPath -ParentPath $allowedRoot) {
            return $fullPath
        }
    }

    throw "$Description rifiutato perche' fuori dai percorsi generati consentiti: $Path"
}

function Get-OnlyWingetProcess {
    Get-Process -Name 'OnlyWinget' -ErrorAction SilentlyContinue
}

function Assert-ExecutableNotLocked {
    param(
        [switch]$KillProcess,
        [string]$ActionName = 'Operazione'
    )

    $running = @(Get-OnlyWingetProcess)
    if ($running.Count -eq 0) {
        return
    }

    if ($KillProcess) {
        foreach ($proc in $running) {
            try {
                Stop-Process -Id $proc.Id -Force -ErrorAction Stop
            }
            catch {
                throw "Impossibile terminare il processo OnlyWinget (PID $($proc.Id)). Chiudi l'app manualmente e riprova."
            }
        }

        Start-Sleep -Milliseconds 300
        $stillRunning = @(Get-OnlyWingetProcess)
        if ($stillRunning.Count -gt 0) {
            $pids = ($stillRunning | ForEach-Object { $_.Id }) -join ', '
            throw "OnlyWinget e ancora in esecuzione (PID: $pids). Chiudi l'app manualmente e riprova."
        }

        return
    }

    $runningPids = ($running | ForEach-Object { $_.Id }) -join ', '
    throw "$ActionName bloccata: OnlyWinget e in esecuzione (PID: $runningPids) e blocca i file di output. Chiudi l'app o rilancia con -StopRunningInstance."
}
