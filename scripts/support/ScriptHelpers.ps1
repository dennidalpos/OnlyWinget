$script:OnlyWingetScriptsRoot = Split-Path $PSScriptRoot -Parent
$script:OnlyWingetRepositoryRoot = Split-Path $script:OnlyWingetScriptsRoot -Parent

function Test-OnlyWingetAutoInstallAllowed {
    param(
        [string]$Description
    )

    if ($env:ONLYWINGET_SKIP_AUTO_INSTALL -eq '1') {
        throw "$Description mancante. Installazione automatica disabilitata da ONLYWINGET_SKIP_AUTO_INSTALL=1."
    }

    Write-Host "Installazione automatica prerequisito: $Description" -ForegroundColor Cyan
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

# Esecuzione interattiva rimossa come da richiesta.


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
