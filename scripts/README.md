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
| `validate-release.ps1` | Validates tag/HEAD/project versions, optionally remote tag SHA and required setup/portable assets. |
| `test-release-validation.ps1` | Isolated Git/tag and sentinel-asset release regression; included in the full gate. |
| `build.ps1` | Direct WinUI build action. |
| `dev.ps1` | Direct app launch action. |
| `package.ps1` | Direct x64 NSIS setup executable and self-contained portable ZIP packaging action. |
| `test-installer-owned-files.ps1` | Compiles and runs an isolated NSIS fixture; verifies removal of distributed files and preservation of unrelated root/nested files. Requires NSIS. |
| `test-package-artifacts.ps1` | Isolated real-NSIS artifact regression: preserves previous outputs on failures, recovers interrupted promotion and rejects corrupt journals/backups. |
| `check.ps1` | Full gate including the installer file ownership regression. Supports `-Fast` (default) and `-Full`. |
| `clean.ps1` | Guarded generated-output cleanup; `-All` also clears NuGet caches, never application data. |
| `test-clean-preserves-data.ps1` | Isolated cleanup regression preserving workspace/settings/preferences/log sentinels; no real cache clearing. |
| `validate-installer-lifecycle.ps1` | Direct elevated clean-host lifecycle validation. |
| `validate-installed-startup.ps1` | Verifies that an installed executable starts and remains responsive. |
| `align-logos.ps1` | Converts the master brand logo from JPEG to standard PNG/ICO formats and distributes them to application and landing assets. |
| `generate-landing-setup.ps1` | Bundles the setup installer and portable ZIP, copies them to the landing page build directory, and updates landing download links. |
| `install-skills.ps1` | Verifies developer skills installed in `.agents/skills`. |
| `sync-win-dev-skills.ps1` | Clones and synchronizes WinUI 3 developer skills from the microsoft/win-dev-skills repository to `.agents/skills`. |

Support files live under `scripts/support/` and are not standalone entrypoints.

Only NSIS setup EXE and self-contained portable ZIP distribution are supported. The unused `PackageMsix` task/script/manifest have been removed; use `run.ps1 -Task Package` for both supported assets.

Packaging is serialized per worktree through `artifacts/.package.lock`. Setup/portable files are staged on the output volume, validated and individually promoted with retained rollback copies and a recovery journal. The next invocation restores interrupted promotions before building; corrupt recovery data is preserved and fails closed. Consume the pair only after successful packaging completion; two file replacements are not one atomic transaction. See [release details](../docs/release.md).
