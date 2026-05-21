function Assert-Command {
    param(
        [string]$Name
    )

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
