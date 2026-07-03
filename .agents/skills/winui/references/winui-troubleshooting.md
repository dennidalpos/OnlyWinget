# WinUI 3 Troubleshooting Guide

This document outlines common compilation, linking, packaging, and runtime issues in Windows App SDK / WinUI 3 desktop applications, along with specific resolutions.

## 1. Compilation & Restore Errors
- **Missing WinRT Types / Code Generation Failures**:
  - *Symptom*: Compilation fails because generated partial class types (e.g., in `.g.i.cs` files) are missing, or attributes like `[ObservableProperty]` are not recognized.
  - *Fix*: Check if `<UseWinUI>true</UseWinUI>` is set in the `.csproj` file. Make sure `Microsoft.Windows.SDK.BuildTools` NuGet package is referenced. Clean the intermediate directories (`bin/` and `obj/`) and execute `dotnet restore` or run `.\scripts\run.ps1 -Task Setup -ForceEvaluate -NonInteractive`.
- **Target Framework Incompatibility**:
  - *Symptom*: Project fails to restore with RID or Framework mismatch errors.
  - *Fix*: The presentation project requires targeting `net10.0-windows10.0.17763.0` (or higher) to reference WinUI 3 APIs, whereas domain or application assemblies can target generic `.net10.0`. Do not mix incompatible target frameworks across project references.

## 2. XAML Parsing and Layout Exceptions
- **Catastrophic Failure `0x8000FFFF`**:
  - *Symptom*: The application crashes immediately upon launch or navigation with a generic COM Exception (Catastrophic Failure).
  - *Fix*: This is typically caused by a failure during `InitializeComponent()` parsing of the XAML file. Check for:
    - Typo in XAML namespaces.
    - Using a type that does not exist or has incorrect casing.
    - Accessing an uninitialized property or binding target during the layout phase.
- **Layout Cycle Detected**:
  - *Symptom*: App hangs or crashes with a `LayoutCycleException`.
  - *Fix*: Occurs when a control triggers a layout measure pass that changes the parent size, causing infinite layout cycles. Review auto-sizing on grids/controls, and avoid nesting containers that dynamically stretch each other without bounds.

## 3. UI Thread & Dispatching Crashes
- **Access Violation / Wrong Thread Exception**:
  - *Symptom*: System crashes when trying to set properties on UI controls from a background callback.
  - *Fix*: WinUI controls can only be accessed from the thread they were created on. Wrap UI updates in a `DispatcherQueue.TryEnqueue()` call.
  ```csharp
  DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
  {
      this.StatusBlock.Text = "Updated state!";
  });
  ```

## 4. Packaging and Deployment Issues
- **App fails to run in Unpackaged mode**:
  - *Symptom*: Standard executable crashes immediately when run outside of Visual Studio.
  - *Fix*: Ensure the project is built as self-contained. Check the `<WindowsPackageType>None</WindowsPackageType>` and `<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>` settings in the `.csproj` file. Ensure that the WinAppSDK runtime installer (`WindowsAppRuntimeInstall-x64.exe`) has been executed on the host, or that the required bootstrapper initialization API is executed at startup.
- **MSIX App Identity issues**:
  - *Symptom*: APIs related to packaged state (like local settings storage or packagelocal assets) throw exceptions when running in unpackaged mode.
  - *Fix*: Abstract access to packaged context or check the package identity before invoking `AppInfo` or `Package.Current` APIs.
