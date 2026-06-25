# Operations

Run commands from the repository root in PowerShell.

## Setup

```powershell
.\scripts\setup.ps1
```

## Fast Local Check

```powershell
.\scripts\format.ps1 -NoRestore
.\scripts\typecheck.ps1 -Configuration Release -NoRestore
.\scripts\test.ps1 -Configuration Release -NoRestore
```

## Full Gate

```powershell
.\scripts\check.ps1 -Configuration Release
```

The full gate restores, formats, lints scripts, typechecks, tests, builds, packages, and writes artifacts under `artifacts/`.

## Common Commands

| Task | Command |
| --- | --- |
| Restore | `.\scripts\setup.ps1` |
| Format check | `.\scripts\format.ps1 -NoRestore` |
| PowerShell lint | `.\scripts\lint.ps1` |
| Typecheck | `.\scripts\typecheck.ps1 -Configuration Release -NoRestore` |
| Test | `.\scripts\test.ps1 -Configuration Release -NoRestore` |
| Build | `.\scripts\build.ps1 -Configuration Release` |
| Run app | `.\scripts\dev.ps1 -Configuration Release` |
| Package setup | `.\scripts\package.ps1 -Configuration Release -NoRestore` |
| Clean outputs | `.\scripts\clean.ps1 -Configuration Release` |

## Optional Validations

Live `winget` smoke tests:

```powershell
.\scripts\test.ps1 -RunWingetSmoke
```

Elevated installer lifecycle validation on a clean Windows host:

```powershell
.\scripts\validate-installer-lifecycle.ps1 -Configuration Release -NoRestore
```

## Requirements

- Windows 10 or Windows 11.
- `winget --version` succeeds for app use and live smoke tests.
- .NET SDK from `global.json`.
- PowerShell 7+.
- WiX Toolset 3.x for packaging and the full gate.
