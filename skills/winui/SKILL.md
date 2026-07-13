---
name: winui
description: Use this skill for WinUI 3, Windows App SDK, Windows desktop app development, migration from UWP/WPF/WinForms, XAML layout, MVVM, Fluent Design, packaging, deployment, testing, performance, and troubleshooting.
---

# WinUI Skill

Use this skill when the task involves WinUI 3, Windows App SDK, Microsoft.UI.Xaml, Windows desktop applications, XAML, MVVM, Fluent Design, MSIX packaging, Windows app lifecycle, app windowing, native Windows UI, migration from UWP/WPF/WinForms, or troubleshooting Windows App SDK projects.

## Core behavior

When activated:
1. Inspect the project structure before changing code.
2. Identify whether the app is:
   - WinUI 3 packaged
   - WinUI 3 unpackaged
   - Windows App SDK desktop app
   - UWP migration target
   - WPF/WinForms modernization target
   - mixed/native interop project
3. Detect target framework, Windows App SDK package version, .NET SDK version, runtime identifiers, MSIX packaging state, app manifest, and project type.
4. Prefer stable Microsoft-supported packages.
5. Use current Microsoft Learn guidance and official NuGet package metadata.
6. Avoid obsolete UWP-only APIs unless the user is explicitly maintaining UWP.
7. Explain every significant migration or package update before applying it.

## Preferred stack

Use the current stable versions of:
- Microsoft.WindowsAppSDK
- Microsoft.Windows.SDK.BuildTools when applicable
- Microsoft.Windows.CsWinRT when applicable
- CommunityToolkit.Mvvm
- CommunityToolkit.WinUI where relevant
- .NET SDK compatible with the selected Windows App SDK
- Visual Studio workloads or Build Tools required for WinUI development

Do not assume the latest version from memory. Always verify.

## Project analysis checklist

Before making code changes, inspect:
- `.sln`
- `.csproj`
- `Package.appxmanifest`
- `app.manifest`
- `App.xaml`
- `App.xaml.cs`
- `MainWindow.xaml`
- `MainWindow.xaml.cs`
- `Directory.Build.props`
- `global.json`
- `packages.lock.json`
- CI files
- MSIX packaging project files
- README and docs

Report:
- detected project type
- current package versions
- outdated packages
- incompatible target frameworks
- missing workloads
- broken XAML namespaces
- packaging issues
- likely runtime/deployment problems

## Coding standards

For WinUI 3 code:
- Use `Microsoft.UI.Xaml`, not legacy `Windows.UI.Xaml`.
- Prefer MVVM separation for non-trivial UI.
- Keep code-behind minimal unless implementing view-specific behavior.
- Use `x:Bind` when appropriate for performance.
- Use `ObservableObject`, `RelayCommand`, and `ObservableProperty` from CommunityToolkit.Mvvm when suitable.
- Avoid blocking the UI thread.
- Use async APIs correctly.
- Handle DPI, theme, accessibility, localization, and high contrast.
- Prefer Fluent Design patterns and built-in WinUI controls.
- Avoid custom controls unless built-in controls are insufficient.

## Migration rules

For UWP to WinUI 3:
1. Map `Windows.UI.Xaml` to `Microsoft.UI.Xaml`.
2. Replace UWP lifecycle assumptions with Windows App SDK lifecycle patterns.
3. Review storage, picker, notification, windowing, packaging, and app identity APIs.
4. Preserve behavior with small, testable changes.
5. Create a migration report.

For WPF/WinForms to WinUI 3:
1. Do not blindly rewrite the entire app.
2. Identify shared business logic.
3. Separate UI-specific code from services.
4. Recommend incremental modernization where safer.
5. Document unsupported or risky API substitutions.

## Troubleshooting priorities

When debugging:
1. Build errors first.
2. Package restore issues.
3. Windows App SDK version mismatches.
4. Target framework incompatibilities.
5. XAML namespace/type resolution.
6. MSIX/app identity issues.
7. Runtime deployment errors.
8. UI threading and dispatcher issues.
9. Native interop or CsWinRT issues.

## Testing and validation

After changes:
1. Run restore.
2. Run build.
3. Run tests if present.
4. Check package warnings.
5. Validate app launch path where possible.
6. Summarize changed files.
7. Summarize remaining manual steps.

## Table & Grid Layout Best Practices

1. **Star Columns in ScrollViewer**:
   - In a horizontal `ScrollViewer`, star columns (`*`) collapse to 0 or their minimum size because they are measured with infinite width.
   - Solve this by managing column widths dynamically in C# via a layout helper (e.g., `TableLayoutHelper` as a `DependencyObject` resource), converting star columns to absolute pixels based on viewport size, and distributing space.
2. **Compact Checkbox Columns**:
   - Reduce checkbox column width to 32px or narrower.
   - Apply `MinWidth="0"`, `MinHeight="0"`, and `Padding="0"` to the `CheckBox` control itself so it centers without extra label-spacing overhead.
3. **Interactive Header Resizing**:
   - Create resize drag handles as transparent overlay elements in column header cells aligned to the right.
   - WinUI 3 `Border` is `sealed`. To set a custom cursor (like `SizeWestEast`) on a layout container, subclass `Grid` (e.g. `CursorGrid : Grid`) and wrap `ProtectedCursor` in a public `Cursor` property.
   - Capture pointer events (`PointerPressed`, `PointerMoved`, etc.) on the handle, update column width properties, and recalculate in real-time.

## Strict Compilation & XAML Bindings

1. **Warning WMC1506**:
   - `{x:Bind Mode=OneWay}` on properties that do not raise change notifications causes warning `WMC1506`, which fails builds under strict `-WarnAsError` flags.
   - Implement `INotifyPropertyChanged` on the host control class (e.g., `UserControl` or `Page`) to suppress the warning, even if you raise notifications manually via `Bindings.Update()`.
2. **Unused Event Warning CS0067**:
   - When implementing interfaces like `INotifyPropertyChanged` but not raising the event directly in C# code, wrap the event declaration with `#pragma warning disable CS0067` and `#pragma warning restore CS0067` to prevent strict compilation failures.

## Safety

Never:
- delete user source files without explicit approval
- run broad cleanup commands against drives or user directories
- overwrite manifests without backup
- force preview SDKs unless requested
- remove package references without explaining impact
- perform unrelated dependency upgrades
