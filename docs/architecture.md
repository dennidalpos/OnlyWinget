# Architecture

OnlyWinget is a WinUI 3 desktop client for local `winget` package workflows and explicit Windows Update scans.

## Layout

- `src/OnlyWinget.Domain`: package identity, presets, batch selection, operation plans, status, and validation primitives.
- `src/OnlyWinget.Application`: use-case orchestration, preset import/export, workspace/source-preference storage contracts, capability contracts, and `winget`/Windows Update ports.
- `src/OnlyWinget.Infrastructure`: SQLite workspace persistence (`EF Core 10`), WinGet CLI execution, Windows Update COM automation and capability probing.
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

The presentation layer references infrastructure strictly for composition via `AppComposition.cs` (`IHostBuilder` DI). No feature page, control, or ViewModel may reference infrastructure directly. The unused URL protocol is retired; composition performs ownership-checked legacy HKCU cleanup. Package input validation uses `WingetInputValidator`. Domain does not reference application, infrastructure, or UI code.

## Bootstrapping & Composition

Application lifecycle and Dependency Injection are managed in `AppComposition.cs` using `Microsoft.Extensions.Hosting` (`IHost` / `IServiceCollection`). All UI services (`IAppSettingsService`, `IConfirmationService`, `IFilePickerService`, `IClipboardService`, `INavigationRegistry`) and orchestrators (`ApplicationStartupOrchestrator`) are registered by interface/type in DI. Serilog forwards structured events through `AppDiagnosticsSerilogSink` to the internal Infrastructure `DiagnosticLogStore`, shared with direct UI diagnostics. It applies diagnostic settings before writing daily UTF-8 logs with 10 MiB rolling segments in `%LOCALAPPDATA%\OnlyWinget\logs\` and retains a separate bounded memory queue. Retention keeps at most 14 owned files and 140 MiB; file failures are exposed to the viewer.

## Local State & Persistence

Primary workspace persistence is stored in an embedded **SQLite** database managed via **Entity Framework Core 10**:

```text
%LOCALAPPDATA%\OnlyWinget\onlywinget.db
```

Upon application startup, `SqliteWorkspaceStore` automatically detects and migrates legacy `%LOCALAPPDATA%\OnlyWinget\workspace-v1.json` data into SQLite transparently. Other local state files include:

```text
%LOCALAPPDATA%\OnlyWinget\source-preferences-v1.json
%LOCALAPPDATA%\OnlyWinget\settings.json
```

Preset exchange supports only `onlywinget.preset.v1`.

The legacy JSON writer and dormant DPAPI services have been retired. Existing legacy workspace and secure-secret files are retained; SQLite keeps its legacy import reader. SQLite entity metadata remains for existing-schema compatibility pending an explicit migration with rollback.

A failed workspace load is reported to the UI and blocks SQLite saves until a successful reload. Startup stops after a load failure to preserve the diagnostic. Bulk preset paste validates the full batch before changing the preset.

Application snapshots, selections, metadata, activity and workflow publication share one dedicated state lock. Async search/update discovery builds local results, then publishes rows and selections together. Workspace loading publishes only after both workspace and source preferences load successfully. No lock spans an await. A single operation semaphore rejects overlapping workflows and persistent preset edits; requested workspace saves wait asynchronously for the active workflow and honor caller cancellation. Preset mutation commands are disabled during every busy workflow.

## Native Interop & Capabilities

Application startup builds the `IHost`, loads the SQLite workspace, checks OS support, Windows edition, display version, UI culture/language (`CultureInfo.CurrentUICulture`) and UAC elevation privileges. `ISystemCapabilityService` probes the `winget` CLI, checks PowerShell 7 Core (`pwsh.exe`) and Windows PowerShell 5.1 (`powershell.exe`) with dynamic fallback, and probes Windows Update COM activation through PowerShell. Startup also lists package sources.

Source defaults are initialized once using the persisted `DefaultSourcesConfigured` flag. Refresh preserves existing URLs and local disabled choices; missing defaults are added only with elevation. Add/remove/reset enforce elevation in Application and presentation. Source mutations and preference saving use the same guarded lifetime and linked cancellation token; saved preferences are published before the final idle notification. Capability and installed-status probes propagate cancellation. See [source operations](operations.md#source-preferences-and-privileges).

- **WinGet**: `WingetPackageSearchService` and `WingetPackageResolver` use `ProcessWingetCommandRunner` with explicit source arguments. Search results are cached per query/source for five minutes. Unknown or nonnumeric versions are left to WinGet's upgrade logic. Operation failures are classified by `WingetErrorClassifier` across locales using standard HRESULT exit codes. Batch results and diagnostics survive rescans; failed or cancelled apply never triggers an automatic rescan. `OperationExecutionCanceledException` derives from `OperationCanceledException` and carries completed/cancelled results without changing executor signatures. Application commits these results and Activity entries before reporting cancellation; successful packages leave the pending rows, while failed/cancelled selections remain available.
- **Windows Update**: `ComWindowsUpdateService` uses `BeginSearch`, `BeginDownload`, and `BeginInstall` off the UI thread. Cancellation requests `RequestAbort`; `CleanUp` waits for WUA to release the callbacks. Installation failures and cancellation do not automatically repeat the operation through PowerShell. PowerShell fallback remains available when COM cannot be activated. Installation matches both update ID and revision, and incomplete result sets fail explicitly. Results and restart warnings survive rescans; failed installations are not followed by automatic rescans. Supports optional updates via `BrowseOnly`.
- **Direct Search Operations**: Packages found via search can be installed directly without requiring inclusion in a preset or modifying existing presets.
- **Process Security**: `ProcessExternalProcessRunner` handles process execution asynchronously using `ProcessStartInfo.ArgumentList` (no shell involved), so arguments are passed as discrete process parameters rather than a concatenated command line. The app runs as invoker (`app.manifest` specifies `asInvoker`) supporting standard non-admin users without elevation prompts, with runtime UAC privilege verification.

## Presentation & MVVM

The WinUI shell is route-driven through `Shell/NavigationRegistry.cs`. User-facing routes are Home, Packages, Updates, Sources, Activity, and Settings. ViewModels use **`CommunityToolkit.Mvvm`**, including field-based `[ObservableProperty]` generators and `[RelayCommand]` where applicable. Feature ViewModels receive workflow state-change events and dispatch presentation updates through the UI dispatcher; command bars use typed `UiCommand` definitions.

Reusable presentation primitives live under `DesignSystem`: `PageScaffold` owns page chrome and responsive spacing, `OnlyWingetCommandBar` renders typed `UiCommand` definitions, and `OnlyWingetResponsivePanel`/`OnlyWingetWrapPanel` provide adaptive layout. State controls (`StatePresenter`) provide consistent inline status and error transitions. `OnlyWingetTable` owns shared header/row columns, `ListView` virtualization with `ItemsStackPanel`, horizontal scrolling, keyboard multi-selection, mixed select-all, UI Automation names, and stable collection binding.

`Controls/OperationTrackerControl` is a persistent top-of-shell banner that shows operation progress and links to the Activity log. It is always visible in `MainWindow` above the page host, inside the `NavigationView`.

Feature ViewModels own operations, cancellation, validation, confirmation, clipboard, settings, and picker orchestration through the UI service collection created in `AppComposition`.


## Installer

The release artifact is an x64 NSIS multi-user setup EXE created from a self-contained `win-x64` publish (`MultiUser.nsh` supporting per-machine `$PROGRAMFILES64` and per-user `$LOCALAPPDATA\Programs\OnlyWinget`). Packaging also produces a matching self-contained x64 portable ZIP.

Packaging generates `InstalledFiles.nsh` from the publish directory. The same file list controls extraction and removal. Uninstall removes only distributed files and empty directories, preserving unrelated files in the chosen destination. Run `scripts/test-installer-owned-files.ps1` to verify this behavior with an isolated NSIS fixture.

## Notes

Keep this file high-level. Put commands in [`operations.md`](operations.md), release steps in [`release.md`](release.md), and open todos in [`../PROJECT_STATUS.json`](../PROJECT_STATUS.json).
