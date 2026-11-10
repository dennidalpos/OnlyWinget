param([string]$ExecutablePath)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-OwnedApplicationProcess {
    param(
        [string[]]$ExecutablePaths,
        # Injectable candidates allow deterministic coverage of inaccessible processes.
        [object[]]$CandidateProcesses = [Diagnostics.Process]::GetProcessesByName('OnlyWinget')
    )

    $paths = @($ExecutablePaths | ForEach-Object { [IO.Path]::GetFullPath($_) })
    foreach ($process in $CandidateProcesses) {
        try {
            # Explicit getters preserve native exceptions that PowerShell property
            # access can otherwise turn into a null result.
            if ($process.get_HasExited()) { continue }
            $module = $process.get_MainModule()
            if ($null -eq $module) { continue }
            $imagePath = $module.FileName
            if ([string]::IsNullOrWhiteSpace($imagePath)) {
                continue
            }
            if ($paths -contains [IO.Path]::GetFullPath($imagePath)) {
                $process
                $process = $null
            }
        }
        catch [System.ComponentModel.Win32Exception],
              [System.InvalidOperationException],
              [System.UnauthorizedAccessException],
              [System.Management.Automation.PropertyNotFoundException] {
            # No verified path means no authority to close this process.
            # Do not probe HasExited again: that property may itself be inaccessible.
            continue
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
