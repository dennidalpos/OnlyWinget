# Operations

Run commands from the repository root in PowerShell 7+.

## Interactive Runner

```powershell
.\scripts\run.ps1
```

Running any direct script without parameters also opens this menu. In automation, use `run.ps1 -Task <name> -NonInteractive`.

## Setup

```powershell
.\scripts\run.ps1 -Task Setup -NonInteractive
```

## Fast Local Check

```powershell
.\scripts\run.ps1 -Task Format -NoRestore -NonInteractive
.\scripts\run.ps1 -Task Typecheck -Configuration Release -NoRestore -NonInteractive
.\scripts\run.ps1 -Task Test -Configuration Release -NoRestore -NonInteractive
```

## Full Gate

```powershell
.\scripts\run.ps1 -Task Check -Configuration Release -NonInteractive
```

The full gate restores, formats, lints scripts, typechecks, tests, builds, packages, and writes artifacts under `artifacts/`. Missing local prerequisites are installed where practical.

## Common Commands

| Task | Command |
| --- | --- |
| Restore | `.\scripts\run.ps1 -Task Setup -NonInteractive` |
| Format check | `.\scripts\run.ps1 -Task Format -NoRestore -NonInteractive` |
| PowerShell lint | `.\scripts\run.ps1 -Task Lint -NonInteractive` |
| Typecheck | `.\scripts\run.ps1 -Task Typecheck -Configuration Release -NoRestore -NonInteractive` |
| Test | `.\scripts\run.ps1 -Task Test -Configuration Release -NoRestore -NonInteractive` |
| Build | `.\scripts\run.ps1 -Task Build -Configuration Release -NonInteractive` |
| Run app | `.\scripts\run.ps1 -Task Dev -Configuration Release -NonInteractive` |
| Package setup | `.\scripts\run.ps1 -Task Package -Configuration Release -NoRestore -NonInteractive` |
| Clean outputs | `.\scripts\run.ps1 -Task Clean -Configuration Release -NonInteractive` |

## Optional Validations

Live `winget` smoke tests:

```powershell
.\scripts\run.ps1 -Task Test -RunWingetSmoke -NonInteractive
```

Elevated installer lifecycle validation on a clean Windows host:

```powershell
.\scripts\run.ps1 -Task ValidateInstallerLifecycle -Configuration Release -NoRestore -NonInteractive
```

## Requirements

- Windows 10 or Windows 11.
- `winget --version` succeeds for app use and live smoke tests.
- PowerShell 7+.
- The scripts install missing build prerequisites where practical: .NET SDK from `global.json`, PSScriptAnalyzer, WiX Toolset 3.x, and the Windows App Runtime redist for packaging.
- Set `ONLYWINGET_SKIP_AUTO_INSTALL=1` to disable automatic installation.
- `WindowsAppRuntimeInstall.exe` can still be supplied explicitly with `-WindowsAppRuntimeInstallerPath` or `ONLYWINGET_WINDOWS_APP_RUNTIME_INSTALLER`.
