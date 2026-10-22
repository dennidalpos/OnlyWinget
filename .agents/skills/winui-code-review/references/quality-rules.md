# Detailed OnlyWinget review topics

Read the edited scope and actual consumers; generic library samples do not override repository conventions.

## State and binding

- Check `FeatureViewModel` activation/deactivation, `StateChanged` handler disposal and `DispatcherQueue` callbacks after a window closes.
- Update existing `ObservableCollection<T>` instances in place (`ObservableCollectionExtensions`) rather than replacing collection references.
- Verify explicit `x:Bind` modes (`OneWay`/`TwoWay`) for mutable state, `UpdateSourceTrigger=PropertyChanged` on `ValidatedField` editors, and `UiCommand` enabled-state updates during active workflows.
- Preset apply/save and navigation (`IPendingNavigationGuard`) form a single consistency boundary: retain rejected drafts, lock accepted-unsaved edits, retry persistence without repeating the in-memory mutation, and preserve the active selection when navigation or preset switching is cancelled. Never treat an unavailable `XamlRoot` as user consent.

## Performance and lifetime

- Keep `Controls/OnlyWingetTable` (`ListView` + `ItemsStackPanel`) virtualization and shared column sizing intact.
- Keep CPU-bound and native COM/CLI work off the UI thread; use asynchronous I/O and `CancellationToken` propagation throughout.
- Verify `CancellationTokenSource`, SQLite/file stream and COM callback (`WindowsUpdateComJob.CleanUp`) disposal on both normal completion and cancellation.

## Ownership and security

- Require discrete `ProcessStartInfo.ArgumentList` parameters and `WingetInputValidator` checks across CLI and preset import boundaries.
- Scope process, window and registry operations to verified executable paths, PIDs and owner HWNDs (`CloseOwnedApplication.ps1`, `UiTestWindowHelpers.ps1`, `LegacyProtocolCleanup.cs`). Preserve unrelated installations, portable copies and modal pickers.

## Theme, accessibility and localization

- Require semantic controls, stable `AutomationProperties.AutomationId` values, accessible names on icon-only controls, keyboard focus order and explicit status text alongside color/badges.
- Verify `ThemeResource` lookup in Light, Dark and High Contrast themes (`DesignSystem/Resources/*.xaml`, `MainWindow.ApplyTheme`).
- Require matching English and Italian entries in `src/OnlyWinget/TextResources.cs` (validated by `tests/OnlyWinget.Tests/TextResourcesTests.cs`).

Sources: [WinUI accessibility](https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility), [dialogs](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/dialogs-and-flyouts/dialogs), [deployment](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/).
