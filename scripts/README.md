# Scripts

Run from the repository root with PowerShell.

`run.ps1` is the consolidated entrypoint. Run it without parameters for an interactive menu, or pass `-Task` for automation:

```powershell
.\scripts\run.ps1
.\scripts\run.ps1 -Task Check -Configuration Release -NonInteractive
```

The scripts bootstrap missing prerequisites where practical:

- .NET SDK from `global.json` via `winget`.
- PSScriptAnalyzer via `Install-Module`.
- WiX Toolset 3.x via `choco` or `winget`.
- Windows App Runtime redist via local NuGet/cache lookup or official redistributable download.

Set `ONLYWINGET_SKIP_AUTO_INSTALL=1` to turn missing-prerequisite installation into a hard failure.

| Script | Purpose |
| --- | --- |
| `run.ps1` | Main interactive and automation entrypoint. |
| `setup.ps1` | Direct restore action; without parameters opens the menu. |
| `format.ps1` | Direct format action; without parameters opens the menu. |
| `lint.ps1` | Direct PowerShell lint action; without parameters opens the menu. |
| `typecheck.ps1` | Direct warnings-as-errors build action; without parameters opens the menu. |
| `test.ps1` | Direct xUnit action; without parameters opens the menu. |
| `ui-test.ps1` | Repeatable WinApp UI navigation, accessibility, picker, source-toggle, scrolling, and responsive-layout checks against a running app PID. |
| `build.ps1` | Direct WinUI build action; without parameters opens the menu. |
| `dev.ps1` | Direct app launch action; without parameters opens the menu. |
| `package.ps1` | Direct x64 MSI/setup and self-contained portable ZIP packaging action; without parameters opens the menu. |
| `check.ps1` | Direct full gate action; without parameters opens the menu. |
| `clean.ps1` | Direct guarded cleanup action; without parameters opens the menu. |
| `validate-installer-lifecycle.ps1` | Direct elevated clean-host lifecycle validation; without parameters opens the menu. |

Support files live under `scripts/support/` and are not standalone entrypoints.
