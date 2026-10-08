# Operations

## Retired URL protocol

`onlywinget://` is no longer supported. The app does not register or dispatch URL actions. At startup and NSIS uninstall, legacy HKCU cleanup requires the old OnlyWinget label, URL marker and exact quoted command for that executable. Another installed/portable copy's handler is preserved. Cleanup failures are logged at startup or produce a nonzero uninstall exit code.

Already removed portable copies and other user profiles can retain legacy handlers (AUDIT-37). Review `HKCU\Software\Classes\onlywinget\shell\open\command` under the affected profile before manual removal; no automatic cross-profile or unrelated-handler deletion is performed.

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

## Source preferences and privileges

Source refresh and startup preserve disabled sources, existing URLs and intentionally removed defaults. Initial configuration adds missing `winget`/`msstore` sources once; it never removes an existing source to replace its endpoint. Without elevation, missing defaults are reported in Activity and existing sources remain usable. A failed add or preference save leaves initialization incomplete so a later refresh can retry.

Add, remove and reset require restarting OnlyWinget as administrator. Their UI commands are disabled without confirmed elevation and expose an EN/IT privilege hint. Refresh, metadata update and local enable/disable preferences remain available without elevation. Enable/disable changes retain the initialization flag and take effect only after preferences are saved successfully.

See [Microsoft's source-command contract](https://learn.microsoft.com/en-us/windows/package-manager/winget/source).

The global tracker Cancel button and the caller token both cancel source update/add/remove/reset and preference saves. Reset saves its cleared preferences once within the guarded operation; idle notification and success activity occur after persistence. If persistence fails, the result explains that native sources changed but local preferences could not be saved, retaining the previous local enable/disable choices. Cancellation cannot undo a source change already completed by WinGet; refresh the source list before deciding whether to retry.

Capability and installed-status probes propagate cancellation instead of reporting unavailable/not installed. See [Microsoft's cancellation guidance](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads).

## Workspace edits and saving

Preset package identity includes both ID and source, ignoring case. The same ID from another source is allowed. Editing keeps the original identity as its target, permits source-only changes and rejects an identity already used by another row.

Persistent preset edits and active-preset changes are rejected while another workflow is running. Wait for the operation to finish before editing; the rejected action leaves workspace state unchanged. Search/update rows and their available selections are published together, so snapshots remain coherent during background discovery.

An already requested workspace save waits for the current operation rather than failing with a busy error. Its caller can cancel the wait without cancelling that operation. Once admitted, save uses the same caller/global cancellation as other workflows. Save errors remain visible.

Preset editors retain drafts after failed validation, rejected edits or saving failures. Apply must succeed and persist before navigating, switching preset/mode or closing the window. If the mutation succeeded but saving failed, fields remain visible and locked; Apply retries only saving. An unapplied draft can be discarded. Dirty drafts remain guarded after the flyout is dismissed; unchanged edits do not prompt. AUDIT-19 retains the interactive validation of these paths.

Synchronization follows Microsoft's [lock guidance](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/lock) and [cancellable semaphore wait](https://learn.microsoft.com/en-us/dotnet/api/system.threading.semaphoreslim.waitasync?view=net-10.0).

The preset selector restores the actual active preset after a rejected/cancelled switch. It still does not request autosave for an accepted change (remaining AUDIT-35). Until corrected, select the preset while idle and explicitly save the workspace to retain that choice across restart.

## Diagnostic logs

Diagnostic enable/level settings apply to both files and the in-memory viewer, including startup events and Verbose logging. Serilog and direct UI diagnostics share one writer. Daily UTF-8 files use `onlywinget-yyyyMMdd.log` with UTC dates under `%LOCALAPPDATA%/OnlyWinget/logs`; memory retains the latest 1000 accepted events.

Clear in the log viewer deletes recognized app daily logs and clears memory only after successful file cleanup. A write or clear failure is shown with its error; original memory entries remain available for copy/export. Activity clear and Undo affect only Activity. Export reports successful writing, picker cancellation or failure separately. Daily file size/retention remains open under AUDIT-36.

The viewer labels, level badges, filters and action outcomes are localized in English/Italian. Clear opens an inline confirmation inside the existing dialog; cancel leaves logs unchanged. Badges use theme resources and explicit level text. Runtime confirmation/picker behavior and High Contrast appearance remain pending under AUDIT-22.

## Windows Update metadata

Windows Update rows show the update's MSRC severity when available and the actual current `RebootRequired` state. Potential reboot behavior during a future installation is not reported as an already required restart. KB article IDs are displayed with one KB prefix for both native COM and PowerShell results. See Microsoft's [severity](https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iupdate-get_msrcseverity) and [reboot-state](https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iupdate2-get_rebootrequired) contracts.

## WinGet batch results and cancellation

Only a successful apply triggers an automatic rescan. Failed or cancelled batches retain results, diagnostics and pending rows. Manual successful/failed rescans also retain the batch results; a failed scan preserves the previous update rows, and changing their selection does not clear its diagnostic.

Cancellation keeps completed successes/failures, records the active package as unconfirmed and marks packages that never started with zero attempts. During preflight validation, already installed packages and validation failures are recorded immediately. Cancelled rows have EN/IT status labels. Cancellation during retry waiting preserves the last command's output and error without starting another attempt. Cancellation does not undo a package change already completed; check installed status before explicitly retrying an unconfirmed package.

The executor continues to throw a cancellation exception: `OperationExecutionCanceledException` inherits `OperationCanceledException`, preserves its token, and exposes `Summary.Results`. Consumers should retain the summary before handling cancellation. The Application retry method selects failed/cancelled results, while exposing or retiring its disconnected UI path remains AUDIT-28. See [Microsoft cooperative cancellation](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads) and [Task cancellation](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/task-cancellation).

## WinGet certificate failures

Certificate failures return the original exit code/output and an actionable diagnostic. Search, discovery and package execution do not automatically reset sources or retry the failed command. Inspect `winget source list` and the affected endpoint/certificate before retrying. An explicit global reset removes custom sources and requires administrative privileges; use it only after reviewing the configuration. See [Microsoft's source-command contract](https://learn.microsoft.com/en-us/windows/package-manager/winget/source).

## Requirements

- Windows 10 or Windows 11.
- `winget --version` succeeds for app use and live smoke tests.
- PowerShell 7+.
- The scripts install missing build prerequisites where practical: .NET SDK from `global.json`, PSScriptAnalyzer, and NSIS 3.x for setup creation.
- Set `ONLYWINGET_SKIP_AUTO_INSTALL=1` to disable automatic installation.
- Run only one packaging task per worktree. A concurrent invocation fails immediately with the path of `artifacts/.package.lock`; an interrupted process releases the operating-system lock automatically.
- Packaging stages and validates both artifacts before replacing final filenames. On promotion failure it restores the previous pair; after interruption, the next invocation recovers it before building. Preserve `.package-recovery` if recovery fails: the error identifies invalid/missing data rather than deleting evidence. Only consume the pair after packaging succeeds; see [release details](release.md).
