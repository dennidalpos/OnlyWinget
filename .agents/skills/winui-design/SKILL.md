---
name: winui-design
description: "Design or adjust OnlyWinget WinUI pages and controls while preserving its shell, table layout, theme and EN/IT resources."
---

# OnlyWinget UI design

Read [AGENTS.md](../../../AGENTS.md) and the [project skill](../onlywinget/SKILL.md) for repository constraints and verified workflows.

Read existing neighboring views and DesignSystem resources before introducing controls or styling.

- Preserve NavigationView routes, the persistent OperationTrackerControl and the shared OnlyWingetTable/ListView selection and column behavior.
- Prefer built-in semantic controls and existing styles. Use ThemeResource brushes and explicit text for meaningful status; inspect Light/Dark/High Contrast when the change affects rendering.
- Use TextResources.cs/Localize for visible and accessible EN/IT text. Keep AutomationId stable and label icon-only controls.
- Editors use TwoWay/PropertyChanged bindings. Opening another editor or leaving a dirty draft must follow the pending-navigation guard.
- Confirm log deletion inline inside the current ContentDialog; do not nest a second dialog for that window.
- Check compact widths, scaling and keyboard focus with real controls rather than adding fixed layout assumptions.

Read [brushes/icons](references/brushes-and-icons.md), [layout review](references/layout-review.md) or [theme/accessibility](references/theme-accessibility.md) only as needed. A gallery search helper may be used if actually installed; this repository does not bundle winui-search.exe. Missing optional search tooling is not a reason to stop source work.

Sources: [WinUI Gallery](https://github.com/microsoft/WinUI-Gallery), [dialog contract](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/dialogs-and-flyouts/dialogs).
