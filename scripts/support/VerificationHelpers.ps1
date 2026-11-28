$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invoke-CheckedNativeCommand {
    param([string]$Command, [string[]]$Arguments)
    $output = & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command failed with exit code $LASTEXITCODE." }
    $output
}

function Assert-ResponsiveStartup {
    param([Diagnostics.Process]$Process, [ValidateRange(1, 300)][int]$WaitSeconds = 8)
    $deadline = [DateTime]::UtcNow.AddSeconds($WaitSeconds)
    do {
        $Process.Refresh()
        if ($Process.HasExited) {
            throw "Application exited during startup with code $($Process.ExitCode)."
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($Process.MainWindowHandle -eq [IntPtr]::Zero -or -not $Process.Responding) {
        throw 'Application did not expose a responsive main window.'
    }
}
