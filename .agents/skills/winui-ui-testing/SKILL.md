---
name: winui-ui-testing
description: "Validate OnlyWinget interactive views, dialogs, picker ownership, persistence and theme using scoped WinApp UI Automation."
---

# OnlyWinget interactive verification

Use with [the project skill](../onlywinget/SKILL.md). Inspect [scripts/ui-test.ps1](../../../scripts/ui-test.ps1), `scripts/support/UiTestWindowHelpers.ps1` and `scripts/support/UiTestStateHelpers.ps1` before driving a running instance. Query `winapp --help` when checking CLI flags.

- Scope every UI Automation action to the verified application PID and main HWND (`.\scripts\ui-test.ps1 -AppPid <PID> -NonInteractive`). Native `PickerHost` / modal dialogs require verifying that the Win32 owner chain reaches the tested main window and PID (`UiTestWindowHelpers.ps1`); never send global `Escape`/`WM_CLOSE` or close an unowned picker.
- Check each `winapp` command exit code immediately and verify process liveness; an early exit (including code 0) or unresponsive window is a failure (`scripts/support/VerificationHelpers.ps1`).
- Ensure `TextBox` edits reach the `FeatureViewModel` via `UpdateSourceTrigger=PropertyChanged` (`ValidatedField`) or explicit focus movement.
- Normal launches use `%LOCALAPPDATA%\OnlyWinget`. For persistence or destructive log-viewer fixtures, launch the target process with `ONLYWINGET_DATA_ROOT` set in that process's environment to a fully qualified temporary directory (redirects `onlywinget.db`, `workspace-v1.json` migration, `source-preferences-v1.json`, `settings.json` and `logs\`). Verify the fixture PID/root and never treat `onlywinget.db` (SQLite) as JSON.
- Persisted behavior requires checking stored SQLite/JSON state or restarting the app against the same `ONLYWINGET_DATA_ROOT`, not screenshots alone. Keep accepted-unsaved edits (locked fields retrying save) distinct from unapplied dirty drafts (`IPendingNavigationGuard`).
- Import-picker cancellation compares row identities, `ToggleState`, `IsSelected` and the selection container's selected item (including collapsed `ComboBox` preset selectors) via `UiTestStateHelpers.ps1`.
- In `Controls/LogViewerDialog`, verify inline `InfoBar` clear confirm/cancel, I/O error surfacing (`DiagnosticLogStore.LastError`), clipboard/export outcomes and Light/Dark/High Contrast themes. Note that `CancellationToken` in `FilePickerService` governs file I/O after picker return, not native modal picker lifetime; cancel or close an owned modal picker before shutting down the app, or send `WM_CLOSE` to the verified owner HWND.
- Store temporary screenshots/logs under ignored `artifacts/tmp/`, clean up fixture scratch files and stop only processes started for the fixture.

## Deterministic UI & ownership regressions (no `winapp` required)

- Native dialog/picker owner-chain regression: `.\scripts\test-ui-dialog-ownership.ps1`
- UIA checkbox/list/collapsed-selector snapshot regression: `.\scripts\test-ui-state-snapshot.ps1`
- Startup responsiveness & TRX verification invariants: `.\scripts\test-verification-invariants.ps1`
- Installed executable startup check: `.\scripts\validate-installed-startup.ps1 -ExePath <path>`

Source: [UI Automation](https://learn.microsoft.com/en-us/windows/win32/winauto/entry-uiauto-win32).
