# Operations

Run commands from the repository root in PowerShell 7+.

## Runner Task Entrypoint

Run tasks via the `run.ps1` script from the repository root:

```powershell
.\scripts\run.ps1 -Task Setup -NonInteractive
```

`run.ps1` requires `-Task`.

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
| Installed startup check | `.\scripts\run.ps1 -Task ValidateInstalledStartup -NonInteractive` |
| Landing downloads | `.\scripts\run.ps1 -Task GenerateLandingSetup -Configuration Release -NoRestore -NonInteractive` |

## Optional Validations

`Clean` removes generated repository outputs. `-All` also removes repository `.vs`/`packages` directories and clears NuGet caches; it always preserves `%LOCALAPPDATA%/OnlyWinget`, including the database, legacy workspace, settings, source preferences and logs. Preview removal with `scripts/clean.ps1 -All -DryRun -NonInteractive`.

The isolated `scripts/test-clean-preserves-data.ps1` regression uses fixture application data and a fixture-only dotnet stub, so it does not clear the machine's NuGet caches. It runs in the full check gate.

Live `winget` smoke tests:

```powershell
.\scripts\run.ps1 -Task Test -Configuration Release -RunWingetSmoke -NonInteractive
```

Elevated installer lifecycle validation on a clean Windows host:

```powershell
.\scripts\run.ps1 -Task ValidateInstallerLifecycle -Configuration Release -NoRestore -NonInteractive
```

UI automation against a running app PID:

```powershell
.\scripts\ui-test.ps1 -AppPid <PID> -NonInteractive
```

Import-picker cancellation targets only newly opened windows whose owner chain reaches the tested main window, with its PID verified. It never terminates `PickerHost` or closes unrelated dialogs. If ownership cannot be established, that check fails without closing the window. The full gate includes `scripts/test-ui-dialog-ownership.ps1`, an isolated native-window ownership regression; real brokered picker compatibility still needs an interactive check.

## WinGet certificate failures

Certificate failures return the original exit code/output and an actionable diagnostic. Search, discovery and package execution do not automatically reset sources or retry the failed command. Inspect `winget source list` and the affected endpoint/certificate before retrying. An explicit global reset removes custom sources and requires administrative privileges; use it only after reviewing the configuration. See [Microsoft's source-command contract](https://learn.microsoft.com/en-us/windows/package-manager/winget/source).

## Requirements

- Windows 10 or Windows 11.
- `winget --version` succeeds for app use and live smoke tests.
- PowerShell 7+.
- The scripts install missing build prerequisites where practical: .NET SDK from `global.json`, PSScriptAnalyzer, and NSIS 3.x for setup creation.
- Set `ONLYWINGET_SKIP_AUTO_INSTALL=1` to disable automatic installation.
- Run only one packaging task per worktree. A concurrent invocation fails immediately with the path of `artifacts/.package.lock`; an interrupted process releases the operating-system lock automatically.
