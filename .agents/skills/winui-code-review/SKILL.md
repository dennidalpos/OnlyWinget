---
name: winui-code-review
description: "Review edited OnlyWinget WinUI XAML and C# for binding, MVVM, accessibility, theme, persistence and native ownership problems."
---

# OnlyWinget UI review

Use with [the project skill](../onlywinget/SKILL.md). Review the edited scope under `src/OnlyWinget/` and its callers after compilation; report concrete findings with file/line location, severity and user impact.

- Accept existing field-based `[ObservableProperty]` generators (`MVVMTK0045` suppressed in `OnlyWinget.csproj`) and typed `UiCommand` metadata (`src/OnlyWinget.Application/Presentation/UiCommand.cs`). Business and persistence operations remain in `OnlyWinget.Application` and `OnlyWinget.Infrastructure`.
- Check explicit `x:Bind` modes (`OneWay`/`TwoWay`) for mutable properties, nullable paths, editor `UpdateSourceTrigger=PropertyChanged` updates (`ValidatedField`), stable `ObservableCollection<T>` instances and single event subscription/disposal across navigation.
- Preset apply/save failures must retain user drafts (`IPendingNavigationGuard`). After an accepted in-memory mutation, if SQLite persistence fails, keep editor fields locked, block competing edits/navigation and retry only saving.
- Every `ContentDialog` requires a valid `XamlRoot`, inherited `RequestedTheme`, single-dialog-per-window coordination and unavailable-root fallback handling (`Services/ConfirmationService.cs`, `Controls/LogViewerDialog.xaml.cs`).
- Check `AutomationProperties.AutomationId`/`Name`, keyboard navigation, explicit status text (`Controls/StatusBadgeControl.xaml`), `ThemeResource` brushes and High Contrast readability. Severity must never rely on color alone.
- Verify EN/IT localization in `src/OnlyWinget/TextResources.cs` and `Services/LocalizeExtension.cs` (`Localize`); do not introduce `.resw`/`.resx` or `ResourceLoader`.
- `async void` is permitted only on UI event handlers. Avoid synchronous blocking (`.Result`/`.Wait()`) and heavy UI-thread work; review `CancellationTokenSource` disposal and `DispatcherQueue` callbacks after window closure.
- Verify `ProcessStartInfo.ArgumentList` usage, `WingetInputValidator` enforcement at CLI/import boundaries before cache lookup or execution, and exact executable/PID/HWND ownership before process, window or registry mutations. Inaccessible/null-module processes must never be treated as owned or closed.
- Standard `.\scripts\run.ps1 -Task Typecheck` runs `dotnet build -warnaserror` with `.NET 10` analyzers (`Directory.Build.props`); it does not load the optional git-ignored `.agents/skills/winui-dev-workflow/analyzer/Microsoft.WindowsAppSDK.Analyzers.dll`.

See [detailed review topics](references/quality-rules.md) for checklist details.
