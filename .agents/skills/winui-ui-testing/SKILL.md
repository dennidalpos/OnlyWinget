---
name: winui-ui-testing
description: "Validate OnlyWinget interactive views, dialogs, picker ownership, persistence and theme using scoped WinApp UI Automation."
---

# OnlyWinget interactive verification

Read [AGENTS.md](../../../AGENTS.md) and the [project skill](../onlywinget/SKILL.md) for repository constraints and verified workflows.

Inspect scripts/ui-test.ps1 and scripts/test-ui-dialog-ownership.ps1 before driving the running app. Query the installed winapp help/schema for supported syntax instead of assuming commands from another version.

- Scope actions to the verified application PID and HWND. PickerHost windows require ownership checks; PID alone is insufficient. Never use global Escape/WM_CLOSE or terminate an unrelated picker.
- Test relevant flows in a batch with concrete assertions. Preserve earlier native failures and check process liveness; a zero early exit is not startup success.
- TextBox updates must reach the ViewModel: use the actual TwoWay/PropertyChanged binding, or move focus when the binding requires it.
- The normal app has no test data-path override and uses a personal SQLite workspace/settings/log directory. Use an isolated profile for destructive/persistence fixtures. Do not treat a .db as JSON.
- Persisted behavior needs a save/restart or equivalent stored-state check, not a screenshot alone. Keep accepted-unsaved edits and failed-save retry distinct from unapplied drafts.
- Import cancellation compares row identities, checkbox states and selected preset through UIA patterns, including collapsed selection containers. scripts/test-ui-state-snapshot.ps1 verifies these comparisons on an isolated window; it does not qualify the real WinUI picker.
- For logs, validate inline confirm/cancel, I/O failure, copy/export outcomes, close during export and theme changes. Shared picker cancellation currently observes the token during file I/O, not native picker lifetime.
- Capture artifacts under ignored artifacts/tmp, remove owned scratch after use, and stop only processes started for the fixture.

AUDIT-10/19/22/27 identify pending validations. Record actual execution separately from compiled XAML and deterministic regressions.

Source: [UI Automation](https://learn.microsoft.com/en-us/windows/win32/winauto/entry-uiauto-win32).
