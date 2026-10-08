---
name: winui
description: "Implement or troubleshoot OnlyWinget WinUI 3 views, binding, windowing, threading and unpackaged deployment."
---

# OnlyWinget WinUI 3

Read [AGENTS.md](../../../AGENTS.md) and the [project skill](../onlywinget/SKILL.md) for repository constraints and verified workflows.

Inspect the affected XAML/code-behind, feature ViewModel and callers before editing. Resolve package/SDK versions from the current project and global.json; preserve those pins unless an upgrade is part of the request.

- Keep WindowsPackageType=None, self-contained win-x64 and the existing app manifest. Build or launch through repository scripts.
- Use Microsoft.UI.Xaml and DispatcherQueue. Keep Application and Domain free of WinUI types.
- Keep business/workflow state in the existing feature/Application boundary; code-behind coordinates views, dialogs and navigation.
- Set binding modes for changing data; preserve TwoWay/PropertyChanged editor bindings, stable collections and captured edit identities.
- Use TextResources.cs/Localize, theme resources and accessible names. An existing ContentDialog must not open a second dialog for the same window; the log viewer uses inline confirmation.
- Tables use ListView virtualization and the repository table controls, not an assumed ItemsRepeater rewrite.
- Treat actual UI rendering, closing, picker ownership and failed-save controls as separate verification from XAML compilation.

Read [current stack](references/winui-current-stack.md), [patterns](references/winui-patterns.md) or [troubleshooting](references/winui-troubleshooting.md) only for the relevant issue. Use existing repository views as examples instead of importing generic application templates.

Sources: [WinUI 3](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/), [Windows App SDK deployment](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/), [dialogs](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/dialogs-and-flyouts/dialogs).
