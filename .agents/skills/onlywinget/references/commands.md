# Repository commands

Run PowerShell 7 from the repository root. [AGENTS.md](../../../../AGENTS.md) records executed command evidence; [scripts/README.md](../../../../scripts/README.md) and [run.ps1](../../../../scripts/run.ps1) define options.

## Choose relevant verification

- Compile: scripts/run.ps1 -Task Typecheck -Configuration Release -NoRestore -NonInteractive
- Offline tests: scripts/run.ps1 -Task Test -Configuration Release -NoRestore -NonInteractive
- Format check: scripts/run.ps1 -Task Format -NoRestore -NonInteractive
- Script lint: scripts/run.ps1 -Task Lint -NonInteractive
- Artifacts: scripts/package.ps1 -NoRestore -Fast -NonInteractive
- Installer ownership/protocol fixture: scripts/test-installer-owned-files.ps1
- Artifact preservation/interruption fixture: scripts/test-package-artifacts.ps1
- Skill inventory: scripts/install-skills.ps1

These are not a mandatory sequence for every edit. Documentation/skill edits need metadata, links and script checks; do not reinstall packages or run live operations for them. Tests are warranted for demonstrated behavior problems and relevant invariants.

Setup restores dependencies. Use it when targets/dependencies changed or restore fails; NU1004 RID changes may require scripts/fix-lockfiles.ps1. Regenerate lockfiles through the toolchain. Format validates by default; -Fix is the explicit mutation.

## Distinguish qualification

Offline tests exclude four live smoke tests. Direct invocation reports those skipped unless explicitly enabled. Fast/Full diagnostics are retained under artifacts/test-results; missing TRX/no executed tests fail. scripts/test-verification-invariants.ps1 covers native/pre-TRX/startup failures. Real UI assertions remain pending under AUDIT-27.

Check is the release gate: restore, cleanup, format, lint, deterministic/native fixtures, compile/tests, packaging and artifact analysis. Its cleanup removes ignored repository outputs; preserve retained evidence before running it. It is not the default skill-edit check.

UI automation uses scripts/ui-test.ps1 -AppPid with a verified fixture PID. Normal Dev launch uses the real personal store; use an isolated profile for destructive/persistence validation. StopRunningInstance is opt-in and requests graceful closure only for generated executable paths in this checkout, failing on refused closure. scripts/test-process-ownership.ps1 validates script/NSIS ownership without launching the real app.

Lifecycle validation changes installation state. CurrentUser is unprivileged; AllUsers needs elevation. Use the direct script for explicit Scope/SkipPackage options because run.ps1 does not forward those parameters. Default validation is clean install/uninstall; genuine PreviousSetupPath is required for upgrade, not a relabeled current payload. Installed-startup validation targets a supplied executable path; verify it before launching.

Live WinGet smoke, real Windows Update install/download, hosted publication and power-loss qualification need their actual environments. List them as unverified when not executed. Do not commit/push or publish from these command examples without the corresponding user request.
