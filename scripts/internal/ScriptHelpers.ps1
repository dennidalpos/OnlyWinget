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
