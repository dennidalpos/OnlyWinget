# Repository commands

Run PowerShell 7 (`pwsh`) from the repository root. [AGENTS.md](../../../../AGENTS.md) defines the mandatory pre-commit gate; [scripts/README.md](../../../../scripts/README.md) and [scripts/run.ps1](../../../../scripts/run.ps1) define all runner parameters.

## Core development and verification commands

- **Restore (locked mode)**: `.\scripts\run.ps1 -Task Setup -NonInteractive` (or `dotnet restore`)
- **Format check / apply**: `.\scripts\run.ps1 -Task Format -NoRestore -NonInteractive` (`-Fix` to apply formatting)
- **PowerShell script lint**: `.\scripts\run.ps1 -Task Lint -NonInteractive`
- **Typecheck (warnings as errors)**: `.\scripts\run.ps1 -Task Typecheck -Configuration Release -NoRestore -NonInteractive`
- **Build Debug / Release**: `dotnet build` or `.\scripts\run.ps1 -Task Build -Configuration Release -NoRestore -NonInteractive`
- **Targeted unit tests**: `dotnet test --filter "FullyQualifiedName~Target" --nologo`
- **Offline test suite (TRX verified)**: `.\scripts\run.ps1 -Task Test -Configuration Release -NoRestore -NonInteractive`
- **Mandatory pre-commit gate**: `dotnet test --nologo && dotnet build -c Release --nologo`
- **Package NSIS setup + portable ZIP**: `.\scripts\run.ps1 -Task Package -Configuration Release -NoRestore -NonInteractive`
- **Full repository gate (21 steps)**: `.\scripts\run.ps1 -Task Check -Configuration Release -NonInteractive`
- **Clean generated outputs**: `.\scripts\run.ps1 -Task Clean -Configuration Release -NonInteractive` (`.\scripts\clean.ps1 -All -DryRun -NonInteractive` to preview NuGet/IDE cache removal)
- **Launch unpackaged app**: `.\scripts\run.ps1 -Task Dev -Configuration Release -NoRestore -NonInteractive`
- **Prepare landing downloads**: `.\scripts\run.ps1 -Task GenerateLandingSetup -Configuration Release -NoRestore -NonInteractive`
- **Regenerate NuGet lockfiles**: `.\scripts\fix-lockfiles.ps1`
- **Verify canonical skills / upstream HEAD**: `.\scripts\install-skills.ps1` and `.\scripts\sync-win-dev-skills.ps1`
- **Refresh branding / NSIS media assets**: `.\scripts\align-logos.ps1` and `.\scripts\update-media-assets.ps1`

## Isolated deterministic regression scripts

- Cleanup data preservation: `.\scripts\test-clean-preserves-data.ps1`
- Process ownership & graceful close: `.\scripts\test-process-ownership.ps1`
- Verification TRX/startup invariants: `.\scripts\test-verification-invariants.ps1`
- Check gate failure diagnostics & NU1004 fixture: `.\scripts\test-check-diagnostics.ps1`
- Media asset dimensions/depth: `.\scripts\test-media-assets.ps1`
- Landing artifact version/preservation: `.\scripts\test-landing-artifacts.ps1`
- UI dialog HWND/PID ownership: `.\scripts\test-ui-dialog-ownership.ps1`
- UI state snapshot (UIA selection/toggle): `.\scripts\test-ui-state-snapshot.ps1`
- Release tag/commit/asset validation: `.\scripts\test-release-validation.ps1`
- NSIS installer owned-file & protocol cleanup: `.\scripts\test-installer-owned-files.ps1`
- Package staging, rollback & recovery journal: `.\scripts\test-package-artifacts.ps1`

## Qualification boundaries

- **Offline vs live smoke tests**: `scripts/test.ps1` excludes `Category=Smoke` by default; direct `dotnet test` skips the 4 `[LiveFact]` tests unless `ONLYWINGET_RUN_WINGET_SMOKE=1` (or `-RunWingetSmoke`) is passed. Both `-Fast` (default) and `-Full` retain logs in `artifacts/test-results/` and fail on missing TRX or zero executed tests.
- **Full `Check` gate**: first deletes `artifacts/` and `tmp/` in the current worktree, runs locked restore (`artifacts/check/restore.log`), format, lint, all 11 isolated regression scripts, typecheck, offline unit tests, build, package, and artifact analysis (`artifacts/build-report.txt`). Preserve any needed evidence under `artifacts/` before running `Check`.
- **Interactive UI automation**: `.\scripts\ui-test.ps1 -AppPid <PID> -NonInteractive` requires a running app instance and `winapp` CLI. For persistence/log tests, launch the target instance with process-scoped `ONLYWINGET_DATA_ROOT` set to a fully qualified temporary directory. `-StopRunningInstance` requests graceful closure only for executables under this checkout's `artifacts/bin`, `artifacts/obj` and `artifacts/publish` (see `PROJECT_STATUS.json` `AUDIT-41` for known limitations on inaccessible processes).
- **Machine-mutating & external checks**: `.\scripts\validate-installer-lifecycle.ps1` (supports `-Scope CurrentUser|AllUsers`, `-SkipPackage`, `-PreviousSetupPath`, `-PreviousVersion`) and `.\scripts\validate-installed-startup.ps1 -ExePath <path>` modify or execute installed state. Live WinGet/Windows Update installations and hosted GitHub Actions runs require their respective target environments; report them as unverified when not executed.
