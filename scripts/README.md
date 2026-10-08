# Scripts

Run from the repository root with PowerShell.

`run.ps1` is the consolidated task runner entrypoint. Pass `-Task` to run tasks:

```powershell
.\scripts\run.ps1 -Task Check -Configuration Release -NonInteractive
```

The scripts bootstrap missing prerequisites where practical:

- .NET SDK from `global.json` via `winget`.
- PSScriptAnalyzer via `Install-Module`.
- NSIS 3.x for setup executable creation.
- Windows App Runtime self-contained bundling.

Set `ONLYWINGET_SKIP_AUTO_INSTALL=1` to turn missing-prerequisite installation into a hard failure.

| Script | Purpose |
| --- | --- |
| `run.ps1` | Main task runner entrypoint. Requires `-Task`. Supports `-Fast` (default) and `-Full` for test tasks. |
| `setup.ps1` | Direct restore action. |
| `format.ps1` | Direct format action. |
| `lint.ps1` | Direct PowerShell lint action. |
| `typecheck.ps1` | Direct warnings-as-errors build action. |
| `test.ps1` | Direct xUnit action. Default `-Fast` returns concise PASS/FAIL + minimal stack trace; use `-Full` for verbose developer logs. |
| `ui-test.ps1` | Repeatable WinApp UI checks against a running app PID. Supports `-Fast` (default) and `-Full`. |
| `test-ui-dialog-ownership.ps1` | Native-window ownership regression: preserves unrelated windows and rejects a mismatched app PID. |
| `test-ui-state-snapshot.ps1` | Real isolated UIA regression: observes checkbox/list/collapsed-preset selection changes and restoration without touching OnlyWinget data. |
| `validate-release.ps1` | Validates tag/HEAD/project versions, optionally remote tag SHA and required setup/portable assets. |
| `test-release-validation.ps1` | Isolated Git/tag and sentinel-asset release regression; included in the full gate. |
| `build.ps1` | Direct WinUI build action. |
| `dev.ps1` | Direct app launch action. |
| `package.ps1` | Direct x64 NSIS setup executable and self-contained portable ZIP packaging action. |
| `test-installer-owned-files.ps1` | Compiles and runs an isolated NSIS fixture; verifies removal of distributed files and preservation of unrelated root/nested files. Requires NSIS. |
| `test-process-ownership.ps1` | Verifies scoped script/NSIS graceful close, preservation of another executable copy and refused-close failure before deleting installation files. |
| `test-package-artifacts.ps1` | Isolated real-NSIS artifact regression: preserves previous outputs on failures, recovers interrupted promotion and rejects corrupt journals/backups. |
| `check.ps1` | Full gate including the installer file ownership regression. Supports `-Fast` (default) and `-Full`. |
| `clean.ps1` | Guarded generated-output cleanup; `-All` also clears NuGet caches, never application data. |
| `test-clean-preserves-data.ps1` | Isolated cleanup regression preserving workspace/settings/preferences/log sentinels; no real cache clearing. |
| `validate-installer-lifecycle.ps1` | Clean install/uninstall; CurrentUser runs unprivileged, AllUsers needs elevation. Upgrade requires a genuine PreviousSetupPath. |
| `validate-installed-startup.ps1` | Verifies that an installed executable starts and remains responsive. |
| `test-verification-invariants.ps1` | Isolated failures: early native/pre-TRX failure, retained Fast/Full logs, early zero exit and missing/responsive startup window. |
| `align-logos.ps1` | Converts the master brand logo from JPEG to standard PNG/ICO formats and distributes them to application and landing assets. |
| `test-media-assets.ps1` | Generates media in an isolated output root and checks consumed NSIS dimensions/depth, readable ICO and exact output files. |
| `generate-landing-setup.ps1` | Bundles the setup installer and portable ZIP, copies them to the landing page build directory, and updates landing download links. |
| `test-landing-artifacts.ps1` | Isolated sentinel regression for exact project-version selection, missing current artifacts and preserved unrelated landing files. |
| `install-skills.ps1` | Read-only verification of all canonical `.agents/skills` entrypoints; does not create duplicate copies. |
| `sync-win-dev-skills.ps1` | Resolves the maintained Microsoft WinUI upstream HEAD and verifies local entrypoints; updates require manual review and are never imported automatically. |

Support files live under `scripts/support/` and are not standalone entrypoints.

Import cancellation captures UIA names/IDs/control types plus ToggleState, IsSelected and the selection container's selected items before and after closing the owned picker. This includes the collapsed preset selector; unchanged labels alone do not prove preserved selection. `test-ui-state-snapshot.ps1` exercises actual checkbox/list/collapsed-selector patterns in a separate WPF fixture, with no OnlyWinget launch or personal-store changes.

`update-media-assets.ps1` defaults to the repository's `assets/logos/logo.png`; a supplied source is used as a complete logo without the former fixed crop. It writes only PNG/ICO app/landing assets and consumed NSIS `HeaderBanner.bmp` (150x57) / `WelcomeDialog.bmp` (164x314), both 24-bit. `-OutputRoot` allows isolated generation; `test-media-assets.ps1` validates that path without modifying tracked branding. No WiX assets or private-machine input path are used.

Landing preparation reads the exact project Version and copies only the matching setup/portable pair. Missing/empty current artifacts or unexpected download-link patterns fail; retained older releases and unrelated landing/build files are preserved. Both prepared files are ignored local artifacts; deploy them together with the HTML only after release checks. Local preparation does not establish deployed-site status.

Offline tests exclude `Category=Smoke`; four live tests report skipped on direct invocation unless explicitly enabled. The duplicate live catalog test was removed and stderr streaming runs deterministically. Both Fast and Full modes retain dotnet output in `artifacts/test-results/*.log` and reject missing TRX, unknown outcomes or zero executed tests. Full also displays output. UI commands check each native exit immediately and assert focus movement, toggle restoration, cancelled-picker row preservation and navigation bounds; real UI qualification remains pending.

`-StopRunningInstance` requests graceful closure only for executable files in this checkout's application bin/obj and installer publish outputs. Other installations and portable copies are preserved. A refused close or unreadable process ownership fails the operation; close the affected app manually and retry. NSIS uninstall uses the same exact-path helper through Windows PowerShell 64-bit and stops before deleting files if closure fails.

Developer skills are maintained only in `.agents/skills`. The former root `skills/` copies and generic application/MSIX templates have been removed. `sync-win-dev-skills.ps1` now preserves local instructions rather than replacing directories. Microsoft moved WinUI skill maintenance to [microsoft/winappCli](https://github.com/microsoft/winappCli), as described by [the original catalog](https://github.com/microsoft/win-dev-skills).

Only NSIS setup EXE and self-contained portable ZIP distribution are supported. The unused `PackageMsix` task/script/manifest have been removed; use `run.ps1 -Task Package` for both supported assets.

Packaging is serialized per worktree through `artifacts/.package.lock`. Setup/portable files are staged on the output volume, validated and individually promoted with retained rollback copies and a recovery journal. The next invocation restores interrupted promotions before building; corrupt recovery data is preserved and fails closed. Consume the pair only after successful packaging completion; two file replacements are not one atomic transaction. See [release details](../docs/release.md).
