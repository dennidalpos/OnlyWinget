# Architecture

OnlyWinget is a WinUI 3 desktop client for local `winget` package workflows and explicit Windows Update scans.

## Layout

- `src/OnlyWinget.Domain`: package identity, presets, batch selection, operation plans, status, and validation primitives.
- `src/OnlyWinget.Application`: use-case orchestration, preset import/export, workspace/source-preference storage contracts, capability contracts, and `winget`/Windows Update ports.
- `src/OnlyWinget.Infrastructure`: SQLite workspace persistence (`EF Core 10`), WinGet CLI execution, Windows Update COM automation, DPAPI secret storage, and capability probing.
- `src/OnlyWinget`: WinUI 3 presentation shell targeting `.NET 10` and Windows 10 build `17763`, configured via `Microsoft.Extensions.Hosting` (`Host.CreateDefaultBuilder()`), Serilog structured logging, and `CommunityToolkit.Mvvm` ViewModels.
- `src/OnlyWinget.Setup`: NSIS setup script and assets, packaged by `scripts/package.ps1`.
- `tests/OnlyWinget.Tests`: xUnit tests for domain, application, infrastructure, and automated UI Automation accessibility audits.
- `scripts`: PowerShell entrypoints.

## Dependency Rule

Dependencies point inward:

```text
WinUI Presentation -> Application -> Domain
Infrastructure -----> Application -> Domain
```

The presentation layer references infrastructure strictly for composition via `AppComposition.cs` (`IHostBuilder` DI). No feature page, control, or ViewModel may reference infrastructure directly. Protocol registration is mediated by `IUrlProtocolService` and input validation by `WingetInputValidator`. Domain does not reference application, infrastructure, or UI code.

## Bootstrapping & Composition

Application lifecycle and Dependency Injection are managed in `AppComposition.cs` using `Microsoft.Extensions.Hosting` (`IHost` / `IServiceCollection`). All UI services (`IAppSettingsService`, `IConfirmationService`, `IFilePickerService`, `IClipboardService`, `INavigationRegistry`, `IUrlProtocolService`) and orchestrators (`ApplicationStartupOrchestrator`) are registered by interface/type in DI. Structured logging is handled via **Serilog** configured with rolling file outputs in `%LOCALAPPDATA%\OnlyWinget\logs\` and an in-memory debug sink `AppDiagnosticsSerilogSink`.

## Local State & Persistence

Primary workspace persistence is stored in an embedded **SQLite** database managed via **Entity Framework Core 10**:

```text
%LOCALAPPDATA%\OnlyWinget\onlywinget.db
```

Upon application startup, `SqliteWorkspaceStore` automatically detects and migrates legacy `%LOCALAPPDATA%\OnlyWinget\workspace-v1.json` data into SQLite transparently. Other local state files include:

```text
%LOCALAPPDATA%\OnlyWinget\source-preferences-v1.json
%LOCALAPPDATA%\OnlyWinget\settings.json
%LOCALAPPDATA%\OnlyWinget\secrets.dpapi
```

Preset exchange supports only `onlywinget.preset.v1`.

A failed workspace load is reported to the UI and blocks SQLite saves until a successful reload. Startup stops after a load failure to preserve the diagnostic. Bulk preset paste validates the full batch before changing the preset.

## Native Interop & Capabilities

Application startup builds the `IHost`, loads the SQLite workspace, checks OS support, probes Windows edition (Home/Pro/Enterprise/IoT), display version, UI culture/language (`CultureInfo.CurrentUICulture`), UAC elevation privileges, probes `winget` COM and CLI capabilities, checks dual PowerShell availability (PowerShell 7 Core `pwsh.exe` and Windows PowerShell 5.1 `powershell.exe` with dynamic fallback), lists sources, and probes Windows Update COM availability (`WUApiLib`) through `ISystemCapabilityService`.

- **WinGet**: `WingetPackageSearchService` and `WingetPackageResolver` use `ProcessWingetCommandRunner` with explicit source arguments. Search results are cached per query/source for five minutes. Unknown or nonnumeric versions are left to WinGet's upgrade logic. Operation failures are classified by `WingetErrorClassifier` across locales using standard HRESULT exit codes.
- **Windows Update**: `ComWindowsUpdateService` uses `BeginSearch`, `BeginDownload`, and `BeginInstall` off the UI thread. Cancellation requests `RequestAbort`; `CleanUp` waits for WUA to release the callbacks. Installation failures and cancellation do not automatically repeat the operation through PowerShell. PowerShell fallback remains available when COM cannot be activated. Installation matches both update ID and revision, and incomplete result sets fail explicitly. Results and restart warnings survive rescans; failed installations are not followed by automatic rescans. Supports optional updates via `BrowseOnly`.
- **Direct Search Operations**: Packages found via search can be installed directly without requiring inclusion in a preset or modifying existing presets.
- **Process Security**: `ProcessExternalProcessRunner` handles process execution asynchronously using `ProcessStartInfo.ArgumentList` (no shell involved), so arguments are passed as discrete process parameters rather than a concatenated command line. The app runs as invoker (`app.manifest` specifies `asInvoker`) supporting standard non-admin users without elevation prompts, with runtime UAC privilege verification.

## Presentation & MVVM

The WinUI shell is route-driven through `Shell/NavigationRegistry.cs`. User-facing routes are Home, Packages, Updates, Sources, Activity, and Settings. All ViewModels utilize **`CommunityToolkit.Mvvm`** (v8.4+) with `[ObservableProperty]` and `[RelayCommand]` source generators, communicating via `WeakReferenceMessenger`.

Reusable presentation primitives live under `DesignSystem`: `PageScaffold` owns page chrome and responsive spacing, `OnlyWingetCommandBar` renders typed `UiCommand` definitions, and `OnlyWingetResponsivePanel`/`OnlyWingetWrapPanel` provide adaptive layout. State controls (`StatePresenter`) provide consistent inline status and error transitions. `OnlyWingetTable` owns shared header/row columns, items virtualization (`ItemsRepeater`), horizontal scrolling, keyboard multi-selection, mixed select-all, UI Automation names, and stable collection binding.

`Controls/OperationTrackerControl` is a persistent top-of-shell banner that shows operation progress and links to the Activity log. It is always visible in `MainWindow` above the page host, inside the `NavigationView`.

Feature ViewModels own operations, cancellation, validation, confirmation, clipboard, settings, and picker orchestration through the UI service collection created in `AppComposition`.


## Installer

The release artifact is an x64 NSIS multi-user setup EXE created from a self-contained `win-x64` publish (`MultiUser.nsh` supporting per-machine `$PROGRAMFILES64` and per-user `$LOCALAPPDATA\Programs\OnlyWinget`). Packaging also produces a matching self-contained x64 portable ZIP.

Packaging generates `InstalledFiles.nsh` from the publish directory. The same file list controls extraction and removal. Uninstall removes only distributed files and empty directories, preserving unrelated files in the chosen destination. Run `scripts/test-installer-owned-files.ps1` to verify this behavior with an isolated NSIS fixture.

## Notes

Keep this file high-level. Put commands in [`operations.md`](operations.md), release steps in [`release.md`](release.md), and open todos in [`../PROJECT_STATUS.json`](../PROJECT_STATUS.json).
