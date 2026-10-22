---
name: winui-design
description: "Design or adjust OnlyWinget WinUI pages and controls while preserving its shell, table layout, theme and EN/IT resources."
---

# OnlyWinget UI design

Use with [the project skill](../onlywinget/SKILL.md). Inspect neighboring views under `src/OnlyWinget/Features/` and shared resources under `src/OnlyWinget/DesignSystem/` and `src/OnlyWinget/Controls/` before introducing controls or styles.

- Preserve `Shell/NavigationRegistry.cs` routes (`Home`, `Packages`, `Updates`, `Sources`, `Activity`, `Settings`), the persistent top banner `Controls/OperationTrackerControl`, `DesignSystem/Controls/PageScaffold` and `Controls/OnlyWingetTable` (`ListView` + `ItemsStackPanel`) selection/column behavior.
- Reuse existing styles in `src/OnlyWinget/DesignSystem/Resources/` (`Buttons.xaml`, `Cards.xaml`, `Forms.xaml`, `Spacing.xaml`, `Tables.xaml`, `Typography.xaml`) and adaptive panels (`OnlyWingetResponsivePanel`, `OnlyWingetWrapPanel`, `StatePresenter`). Use `{ThemeResource ...}` brushes and explicit status text (`Controls/StatusBadgeControl`).
- Use `src/OnlyWinget/TextResources.cs` and `Services/LocalizeExtension.cs` (`Localize`) for visible and accessible EN/IT text. Keep `AutomationProperties.AutomationId` stable and set `AutomationProperties.Name` on icon-only controls.
- Editor fields use `TwoWay` bindings with `UpdateSourceTrigger=PropertyChanged`. Opening another editor or leaving a dirty draft must honor `IPendingNavigationGuard`.
- Confirm log deletion inline via the `InfoBar` inside `Controls/LogViewerDialog`; never open a nested `ContentDialog` on the same window.
- Branding and NSIS installer media assets are generated from `assets/logos/logo.png` via `.\scripts\align-logos.ps1` and `.\scripts\update-media-assets.ps1` (verified by `.\scripts\test-media-assets.ps1`).

## References and optional helpers

- [Brushes and icons](references/brushes-and-icons.md), [layout review](references/layout-review.md) and [theme/accessibility](references/theme-accessibility.md).
- `.agents/skills/winui-design/winui-search.exe` is an optional git-ignored upstream WinUI Gallery search helper (preserved in local checkouts when present, not tracked in Git). Use `.agents/skills/winui-design/winui-search.exe search "<query>"` when present locally; do not block design or source work if absent in a fresh clone.

Sources: [WinUI Gallery](https://github.com/microsoft/WinUI-Gallery), [dialog contract](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/dialogs-and-flyouts/dialogs).
