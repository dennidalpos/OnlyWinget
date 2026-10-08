param([string]$ExecutablePath)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-OwnedApplicationProcess {
    param([string[]]$ExecutablePaths)

    $paths = @($ExecutablePaths | ForEach-Object { [IO.Path]::GetFullPath($_) })
    foreach ($process in [Diagnostics.Process]::GetProcessesByName('OnlyWinget')) {
        try {
            if ($process.HasExited) { continue }
            $imagePath = $process.MainModule.FileName
            if ([string]::IsNullOrWhiteSpace($imagePath)) {
                throw "Cannot verify executable ownership for PID $($process.Id)."
            }
            if ($paths -contains [IO.Path]::GetFullPath($imagePath)) {
                $process
                $process = $null
            }
        }
        catch [System.InvalidOperationException] {
            if (-not $process.HasExited) { throw }
        }
        finally {
            if ($null -ne $process) { $process.Dispose() }
        }
    }
}

function Close-OwnedApplicationProcess {
    param([Diagnostics.Process[]]$Processes)

    foreach ($process in $Processes) {
        if ($process.HasExited) { continue }
        # Respect the application's pending-edit guard; never force termination.
        if (-not $process.CloseMainWindow() -or -not $process.WaitForExit(5000)) {
            throw "OnlyWinget (PID $($process.Id)) did not close. Close it manually and retry."
        }
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    if ([string]::IsNullOrWhiteSpace($ExecutablePath)) { throw 'ExecutablePath is required.' }
    $owned = @(Get-OwnedApplicationProcess -ExecutablePaths @($ExecutablePath))
    try { Close-OwnedApplicationProcess -Processes $owned }
    finally { foreach ($process in $owned) { $process.Dispose() } }
}
