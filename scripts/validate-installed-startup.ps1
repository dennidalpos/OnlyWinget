param(
    [string]$ExePath = 'C:\Program Files\OnlyWinget\OnlyWinget.exe',
    [int]$WaitSeconds = 8,
    [switch]$LeaveRunning,
    [switch]$NonInteractive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/ScriptHelpers.ps1')

Assert-Path -Path $ExePath -Description 'OnlyWinget installed executable'

$startTime = Get-Date
$process = Start-Process -FilePath $ExePath -PassThru -WindowStyle Normal
Start-Sleep -Seconds $WaitSeconds

$process.Refresh()
if ($process.HasExited -and $process.ExitCode -ne 0) {
    throw "OnlyWinget exited during startup smoke test with code $($process.ExitCode)."
}

$events = @(
    Get-WinEvent -FilterHashtable @{
        LogName = 'Application'
        StartTime = $startTime
    } -ErrorAction SilentlyContinue |
        Where-Object {
            ($_.ProviderName -in @('Application Error', 'Windows Error Reporting', '.NET Runtime')) -and
            $_.Message -like '*OnlyWinget.exe*'
        } |
        Select-Object TimeCreated, ProviderName, Id, LevelDisplayName, Message
)

if (-not $LeaveRunning -and -not $process.HasExited) {
    Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
}

if ($events.Count -gt 0) {
    $summary = $events |
        ForEach-Object { "$($_.TimeCreated) [$($_.ProviderName)/$($_.Id)] $($_.LevelDisplayName)" }
    throw "OnlyWinget startup produced Windows error events:`n$($summary -join [Environment]::NewLine)"
}

Write-Host "OnlyWinget installed startup smoke passed: $ExePath" -ForegroundColor Green
