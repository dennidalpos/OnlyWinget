---
name: winui
description: "Implement or troubleshoot OnlyWinget WinUI 3 views, binding, windowing, threading and unpackaged deployment."
---

# OnlyWinget WinUI 3

Use with [the project skill](../onlywinget/SKILL.md). Inspect the affected XAML/code-behind under `src/OnlyWinget/`, its `FeatureViewModel` and `src/OnlyWinget/AppComposition.cs` before editing. Resolve SDK and package pins from [global.json](../../../global.json) and [OnlyWinget.csproj](../../../src/OnlyWinget/OnlyWinget.csproj).

- Keep `WindowsPackageType=None`, self-contained `win-x64` and `src/OnlyWinget/app.manifest` (`asInvoker`). Build or launch through `scripts/run.ps1`.
- Use `Microsoft.UI.Xaml` and `DispatcherQueue`. Keep `OnlyWinget.Application` and `OnlyWinget.Domain` free of WinUI types.
- Keep workflow state in `OnlyWingetApplication`; code-behind coordinates views, flyouts, `ContentDialog` `XamlRoot` assignment and `IPendingNavigationGuard` checks (`src/OnlyWinget/Shell/NavigationRegistry.cs`).
- Set explicit binding modes (`OneWay`/`TwoWay`) for changing data; preserve `UpdateSourceTrigger=PropertyChanged` on editor `TextBox` bindings, stable `ObservableCollection<T>` instances and captured edit identities.
- Use `src/OnlyWinget/TextResources.cs` and `Services/LocalizeExtension.cs` (`Localize`), `ThemeResource` brushes and accessible names (`AutomationProperties.Name`/`AutomationId`). Only one `ContentDialog` may be open per window at a time; `Controls/LogViewerDialog` uses inline `InfoBar` confirmation for log deletion.
- Tables use `Controls/OnlyWingetTable` (`ListView` + `ItemsStackPanel` virtualization), not `ItemsRepeater`.

## References and diagnostics

- [Current stack](references/winui-current-stack.md), [view patterns](references/winui-patterns.md) and [troubleshooting](references/winui-troubleshooting.md).
- Environment diagnostic probe: `pwsh -NoProfile -File .agents/skills/winui/scripts/check-winui-environment.ps1`.

Sources: [WinUI 3](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/), [Windows App SDK deployment](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/), [dialogs](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/dialogs-and-flyouts/dialogs).
