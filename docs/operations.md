# Operations

Run commands from the repository root in PowerShell 7+.

## Runner Task Entrypoint

Run tasks via the `run.ps1` script from the repository root:

```powershell
.\scripts\run.ps1 -Task Setup
```

Running any direct script under `scripts/` without parameters executes its direct/default task immediately and non-interactively.

## Setup

```powershell
.\scripts\run.ps1 -Task Setup
```

## Fast Local Check

```powershell
.\scripts\run.ps1 -Task Format -NoRestore
.\scripts\run.ps1 -Task Typecheck -Configuration Release -NoRestore
.\scripts\run.ps1 -Task Test -Configuration Release -NoRestore
```

## Full Gate

```powershell
.\scripts\run.ps1 -Task Check -Configuration Release
```

The full gate restores, formats, lints scripts, typechecks, tests, builds, packages, and writes artifacts under `artifacts/`. Missing local prerequisites are installed where practical.

## Common Commands

| Task | Command |
| --- | --- |
| Restore | `.\scripts\run.ps1 -Task Setup` |
| Format check | `.\scripts\run.ps1 -Task Format -NoRestore` |
| PowerShell lint | `.\scripts\run.ps1 -Task Lint` |
| Typecheck | `.\scripts\run.ps1 -Task Typecheck -Configuration Release -NoRestore` |
| Test | `.\scripts\run.ps1 -Task Test -Configuration Release -NoRestore` |
| Build | `.\scripts\run.ps1 -Task Build -Configuration Release` |
| Run app | `.\scripts\run.ps1 -Task Dev -Configuration Release` |
| Package setup | `.\scripts\run.ps1 -Task Package -Configuration Release -NoRestore` |
| Clean outputs | `.\scripts\run.ps1 -Task Clean -Configuration Release` |

## Optional Validations

Live `winget` smoke tests:

```powershell
.\scripts\run.ps1 -Task Test -RunWingetSmoke
```

Elevated installer lifecycle validation on a clean Windows host:

```powershell
.\scripts\run.ps1 -Task ValidateInstallerLifecycle -Configuration Release -NoRestore
```

## Requirements

- Windows 10 or Windows 11.
- `winget --version` succeeds for app use and live smoke tests.
- PowerShell 7+.
- The scripts install missing build prerequisites where practical: .NET SDK from `global.json`, PSScriptAnalyzer, WiX Toolset 3.x, and the Windows App Runtime redist for packaging.
- Set `ONLYWINGET_SKIP_AUTO_INSTALL=1` to disable automatic installation.
- `WindowsAppRuntimeInstall.exe` can still be supplied explicitly with `-WindowsAppRuntimeInstallerPath` or `ONLYWINGET_WINDOWS_APP_RUNTIME_INSTALLER`.
- Downloaded Windows App Runtime redistributables are cached under `dependencies/windowsappsdk/`, outside `artifacts`, so clean scripts do not remove reusable installer prerequisites.
- Run only one packaging task per worktree. A concurrent invocation fails immediately with the path of `artifacts/.package.lock`; an interrupted process releases the operating-system lock automatically.
