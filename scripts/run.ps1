param(
    [ValidateSet(
        'Setup',
        'Format',
        'Lint',
        'Typecheck',
        'Test',
        'Build',
        'Package',
        'Check',
        'Clean',
        'Dev',
        'ValidateInstallerLifecycle',
        'ValidateInstalledStartup',
        'GenerateLandingSetup'
    )]
    [string]$Task,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$WindowsAppRuntimeInstallerPath = $env:ONLYWINGET_WINDOWS_APP_RUNTIME_INSTALLER,
    [string]$InstalledExePath = 'C:\Program Files\OnlyWinget\OnlyWinget.exe',
    [switch]$Fix,
    [switch]$ForceEvaluate,
    [switch]$NoRestore,
    [switch]$NoBuild,
    [switch]$RunWingetSmoke,
    [switch]$StopRunningInstance,
    [switch]$SkipBundle,
    [switch]$All,
    [switch]$NuGetCache,
    [switch]$NonInteractive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/ScriptHelpers.ps1')

function Read-MenuChoice {
    param(
        [string]$Title,
        [string[]]$Choices
    )

    Write-Host ''
    Write-Host $Title -ForegroundColor Cyan
    for ($index = 0; $index -lt $Choices.Count; $index++) {
        Write-Host (" {0}. {1}" -f ($index + 1), $Choices[$index])
    }

    do {
        $rawChoice = Read-Host 'Selezione'
        $parsedChoice = 0
        $isNumber = [int]::TryParse($rawChoice, [ref]$parsedChoice)
    } while (-not $isNumber -or $parsedChoice -lt 1 -or $parsedChoice -gt $Choices.Count)

    return $Choices[$parsedChoice - 1]
}

function Read-ConfigurationChoice {
    $choice = Read-MenuChoice -Title 'Configurazione' -Choices @('Release', 'Debug')
    return $choice
}

function Read-OptionalSwitch {
    param(
        [string]$Prompt,
        [bool]$DefaultYes = $false
    )

    return Read-OnlyWingetYesNo -Prompt $Prompt -DefaultYes $DefaultYes
}

function Add-CommonPackageParameter {
    param(
        [hashtable]$Parameters
    )

    if (-not [string]::IsNullOrWhiteSpace($WindowsAppRuntimeInstallerPath)) {
        $Parameters.WindowsAppRuntimeInstallerPath = $WindowsAppRuntimeInstallerPath
    }
}

function Invoke-OnlyWingetTask {
    param(
        [string]$SelectedTask
    )

    switch ($SelectedTask) {
        'Setup' {
            & (Join-Path $PSScriptRoot 'setup.ps1') -ForceEvaluate:$ForceEvaluate -NonInteractive
        }
        'Format' {
            & (Join-Path $PSScriptRoot 'format.ps1') -Fix:$Fix -NoRestore:$NoRestore -NonInteractive
        }
        'Lint' {
            & (Join-Path $PSScriptRoot 'lint.ps1') -Required -NonInteractive
        }
        'Typecheck' {
            & (Join-Path $PSScriptRoot 'typecheck.ps1') -Configuration $Configuration -NoRestore:$NoRestore -StopRunningInstance:$StopRunningInstance -NonInteractive
        }
        'Test' {
            & (Join-Path $PSScriptRoot 'test.ps1') -Configuration $Configuration -NoRestore:$NoRestore -NoBuild:$NoBuild -RunWingetSmoke:$RunWingetSmoke -NonInteractive
        }
        'Build' {
            & (Join-Path $PSScriptRoot 'build.ps1') -Configuration $Configuration -NoRestore:$NoRestore -StopRunningInstance:$StopRunningInstance -NonInteractive
        }
        'Package' {
            $parameters = @{
                Configuration = $Configuration
                NoRestore = $NoRestore
                StopRunningInstance = $StopRunningInstance
                SkipBundle = $SkipBundle
            }
            Add-CommonPackageParameter -Parameters $parameters
            & (Join-Path $PSScriptRoot 'package.ps1') @parameters -NonInteractive
        }
        'Check' {
            $parameters = @{
                Configuration = $Configuration
                RunWingetSmoke = $RunWingetSmoke
            }
            Add-CommonPackageParameter -Parameters $parameters
            & (Join-Path $PSScriptRoot 'check.ps1') @parameters -NonInteractive
        }
        'Clean' {
            & (Join-Path $PSScriptRoot 'clean.ps1') -Configuration $Configuration -StopRunningInstance:$StopRunningInstance -All:$All -NuGetCache:$NuGetCache -NonInteractive
        }
        'Dev' {
            & (Join-Path $PSScriptRoot 'dev.ps1') -Configuration $Configuration -Build:(-not $NoBuild) -NoRestore:$NoRestore -StopRunningInstance:$StopRunningInstance -NonInteractive
        }
        'ValidateInstallerLifecycle' {
            $parameters = @{
                Configuration = $Configuration
                NoRestore = $NoRestore
            }
            Add-CommonPackageParameter -Parameters $parameters
            & (Join-Path $PSScriptRoot 'validate-installer-lifecycle.ps1') @parameters -NonInteractive
        }
        'ValidateInstalledStartup' {
            $scriptPath = Join-Path $PSScriptRoot 'validate-installed-startup.ps1'
            Assert-Path -Path $scriptPath -Description 'Installed startup validation script'
            & $scriptPath -ExePath $InstalledExePath -NonInteractive
        }
        'GenerateLandingSetup' {
            $parameters = @{
                Configuration = $Configuration
                NoRestore = $NoRestore
            }
            Add-CommonPackageParameter -Parameters $parameters
            & (Join-Path $PSScriptRoot 'generate-landing-setup.ps1') @parameters -NonInteractive
        }
    }
}

if ([string]::IsNullOrWhiteSpace($Task)) {
    if ($NonInteractive) {
        throw 'Passa -Task quando usi run.ps1 in modalita non interattiva.'
    }

    $Task = Read-MenuChoice -Title 'OnlyWinget task' -Choices @(
        'Setup',
        'Format',
        'Lint',
        'Typecheck',
        'Test',
        'Build',
        'Package',
        'Check',
        'Clean',
        'Dev',
        'ValidateInstallerLifecycle',
        'ValidateInstalledStartup',
        'GenerateLandingSetup'
    )

    if ($Task -in @('Typecheck', 'Test', 'Build', 'Package', 'Check', 'Clean', 'Dev', 'ValidateInstallerLifecycle', 'GenerateLandingSetup')) {
        $Configuration = Read-ConfigurationChoice
    }

    if ($Task -eq 'Format') {
        $Fix = Read-OptionalSwitch -Prompt 'Applicare la formattazione invece di verificarla?' -DefaultYes:$false
    }

    if ($Task -eq 'Test' -or $Task -eq 'Check') {
        $RunWingetSmoke = Read-OptionalSwitch -Prompt 'Eseguire anche gli smoke test winget reali?' -DefaultYes:$false
    }

    if ($Task -in @('Package', 'Check', 'ValidateInstallerLifecycle', 'GenerateLandingSetup')) {
        $providedRuntimeInstaller = Read-Host 'Percorso WindowsAppRuntimeInstall.exe (invio per auto-download/cache/env)'
        if (-not [string]::IsNullOrWhiteSpace($providedRuntimeInstaller)) {
            $WindowsAppRuntimeInstallerPath = $providedRuntimeInstaller
        }
    }

    if ($Task -eq 'Clean') {
        $All = Read-OptionalSwitch -Prompt 'Pulizia aggressiva anche di .vs e packages?' -DefaultYes:$false
        $NuGetCache = Read-OptionalSwitch -Prompt 'Pulire anche la cache NuGet/.NET?' -DefaultYes:$false
    }
}

Invoke-OnlyWingetTask -SelectedTask $Task
