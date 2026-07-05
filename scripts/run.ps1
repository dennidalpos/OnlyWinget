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

# Funzioni di menu interattivo rimosse come da richiesta.

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
    throw 'Il parametro -Task e'' obbligatorio. L''esecuzione interattiva e'' disabilitata. / The -Task parameter is required. Interactive execution is disabled.'
}

Invoke-OnlyWingetTask -SelectedTask $Task
