---
name: winui-wpf-migration
description: "Use only for explicitly requested WPF-to-WinUI migration work in OnlyWinget; preserve its existing unpackaged architecture."
---

# Migration work in OnlyWinget

Use with [the project skill](../onlywinget/SKILL.md) only when the user explicitly requests importing or migrating WPF views/components into OnlyWinget.

OnlyWinget is already a `.NET 10` WinUI 3 application (`src/OnlyWinget/OnlyWinget.csproj`). Do not initiate a framework migration, regenerate the project or add WPF/MSIX project references during normal work.

For an explicitly requested WPF-to-WinUI import:

- Inspect source controls, bindings, commands, resources and behavior before selecting replacements.
- Map `System.Windows` types to `Microsoft.UI.Xaml`, `DispatcherQueue` and explicit Win32 HWND ownership (`WinRT.Interop.WindowNative` / `InitializeWithWindow`).
- Reuse `Controls/OnlyWingetTable` (`ListView` + `ItemsStackPanel`), `DesignSystem/Controls/PageScaffold`, `DesignSystem/Commands/OnlyWingetCommandBar`, `Presentation/FeatureViewModel` and `OnlyWinget.Application` ports. Keep process execution and SQLite/JSON persistence out of presentation views.
- Add bilingual EN/IT strings to `src/OnlyWinget/TextResources.cs` and bind via `Services/LocalizeExtension.cs` (`Localize`). Do not introduce `.resw`/`.resx` or `ResourceLoader`.
- Preserve `WindowsPackageType=None`, `src/OnlyWinget/app.manifest` (`asInvoker`), self-contained `win-x64` and NSIS/portable packaging (`src/OnlyWinget.Setup/OnlyWinget.nsi`).

Source: [Windows app migration guidance](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/).
