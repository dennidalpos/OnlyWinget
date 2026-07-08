# Scripts

Run from the repository root with PowerShell.

`run.ps1` is the consolidated task runner entrypoint. Pass `-Task` to run tasks:

```powershell
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
| `run.ps1` | Main task runner entrypoint. Requires `-Task`. |
| `setup.ps1` | Direct restore action. |
| `format.ps1` | Direct format action. |
| `lint.ps1` | Direct PowerShell lint action. |
| `typecheck.ps1` | Direct warnings-as-errors build action. |
| `test.ps1` | Direct xUnit action. |
| `ui-test.ps1` | Repeatable WinApp UI navigation, accessibility, picker, source-toggle, scrolling, and responsive-layout checks against a running app PID. |
| `build.ps1` | Direct WinUI build action. |
| `dev.ps1` | Direct app launch action. |
| `package.ps1` | Direct x64 MSI/setup and self-contained portable ZIP packaging action. |
| `check.ps1` | Direct full gate action. |
| `clean.ps1` | Direct guarded cleanup action. |
| `validate-installer-lifecycle.ps1` | Direct elevated clean-host lifecycle validation. |
| `validate-installed-startup.ps1` | Verifies that an installed executable starts and remains responsive. |
| `align-logos.ps1` | Converts the master brand logo from JPEG to standard PNG/ICO formats and distributes them to application and landing assets. |
| `generate-landing-setup.ps1` | Bundles the setup installer and portable ZIP, copies them to the landing page build directory, and updates landing download links. |
| `install-skills.ps1` | Installs the repository's OnlyWinget skill into `.agents/skills`. |
| `sync-win-dev-skills.ps1` | Clones and synchronizes WinUI 3 developer skills from the microsoft/win-dev-skills repository to `.agents/skills`. |

Support files live under `scripts/support/` and are not standalone entrypoints.

Packaging is serialized per worktree through `artifacts/.package.lock`. WiX writes MSI and setup outputs in staging and moves them into `artifacts/dist/` only after a successful link, so failed or overlapping runs cannot leave a partially replaced release artifact. WiX 3 ICE03 is disabled because its locale table rejects valid Windows App SDK 2.x MUI locales; all other linker validation remains enabled except the existing ICE61 suppression.
