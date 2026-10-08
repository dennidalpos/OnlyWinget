---
name: winui-wpf-migration
description: "Use only for explicitly requested WPF-to-WinUI migration work in OnlyWinget; preserve its existing unpackaged architecture."
---

# Migration work in OnlyWinget

Read [AGENTS.md](../../../AGENTS.md) and the [project skill](../onlywinget/SKILL.md) for repository constraints and verified workflows.

OnlyWinget is already WinUI 3. Do not start a WPF migration, regenerate the app or add another UI framework during ordinary maintenance.

For an explicitly requested migration/import:

- Inspect source controls, bindings, commands, resources and public behavior before selecting replacements.
- Map System.Windows UI types to Microsoft.UI.Xaml, with DispatcherQueue and native window ownership.
- Reuse OnlyWingetTable/ListView, existing feature ViewModels and Application ports. Keep external execution/persistence outside the imported view.
- Translate resources into TextResources.cs/Localize EN/IT. Do not introduce RESW/RESX or ResourceLoader from a generic migration recipe.
- Preserve WindowsPackageType=None, app.manifest, self-contained win-x64 and NSIS/portable outputs. There is no required Package.appxmanifest in this checkout.
- Migrate in reviewable changes and run the relevant compile/invariant check; interactive behavior needs separate validation.

Source: [Windows app migration guidance](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/).
