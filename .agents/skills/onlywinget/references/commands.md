# Repository commands

Run PowerShell 7 (`pwsh`) from the repository root. Read [AGENTS.md](../../../../AGENTS.md) for the repository integration, [operations](../../../../docs/operations.md) for the pre-commit gate, and [scripts/README.md](../../../../scripts/README.md) / [scripts/run.ps1](../../../../scripts/run.ps1) for supported parameters.

Managed runs keep operational evidence in the resolved SecondBrain runtime. Current scripts default to checkout-local `artifacts/` or `tmp/`; they do not share a universal output override. Direct MSBuild `-p:ArtifactsRoot=<absolute-directory>/` redirects .NET outputs only; resource tests also need their source fixture reachable from the binary directory. Record any remaining output exception rather than assuming all scripts use that override. `Clean` and the cleanup performed by `Check` require an explicit cleanup request and preservation of needed evidence.

## Core development and verification commands

- **Restore (locked mode)**: `.\scripts\run.ps1 -Task Setup -NonInteractive` (or `dotnet restore --locked-mode`)
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

- **Offline vs live smoke tests**: `scripts/test.ps1` excludes `Category=Smoke` by default; direct `dotnet test` skips the 4 `[LiveFact]` tests unless the process environment contains `ONLYWINGET_RUN_WINGET_SMOKE=1`. The task runner exposes `-RunWingetSmoke` for that opt-in. Both `-Fast` (default) and `-Full` retain logs in `artifacts/test-results/` and fail on missing TRX or zero executed tests.
- **Full `Check` gate**: first deletes `artifacts/` and `tmp/` in the current worktree, runs locked restore (`artifacts/check/restore.log`), format, lint, all 11 isolated regression scripts, typecheck, offline unit tests, build, package, and artifact analysis (`artifacts/build-report.txt`). Preserve needed evidence outside those directories before an authorized `Check` run.
- **Interactive UI automation**: `.\scripts\ui-test.ps1 -AppPid <PID> -NonInteractive` requires a running app instance and `winapp` CLI. For persistence/log tests, launch the target instance with process-scoped `ONLYWINGET_DATA_ROOT` set to a fully qualified temporary directory. `-StopRunningInstance` requests graceful closure only for verified executables under this checkout's `artifacts/bin`, `artifacts/obj` and `artifacts/publish`; inaccessible/null-module candidates are ignored without closure, not certified as unlocked.
- **Machine-mutating & external checks**: `.\scripts\validate-installer-lifecycle.ps1` defaults to elevated `AllUsers`; pass `-Scope CurrentUser` directly for an unprivileged per-user run. It also supports `-SkipPackage`, `-PreviousSetupPath` and `-PreviousVersion`. The consolidated runner does not expose `-Scope`. Lifecycle validation and `.\scripts\validate-installed-startup.ps1 -ExePath <path>` modify or execute installed state. Live WinGet/Windows Update installations and hosted GitHub Actions runs require their respective target environments; report them as unverified when not executed.
