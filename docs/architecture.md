# Architecture

OnlyWinget is a WinUI 3 desktop client for local `winget` package workflows and explicit Windows Update scans.

## Layout

- `src/OnlyWinget.Domain`: package identity, presets, batch selection, operation plans, status, and validation primitives.
- `src/OnlyWinget.Application`: use-case orchestration, preset import/export, workspace/source-preference storage contracts, capability contracts, and `winget`/Windows Update ports.
- `src/OnlyWinget.Infrastructure`: SQLite relateral workspace persistence (`EF Core 10`), native WinGet COM API (`Microsoft.Management.Deployment`), direct Windows Update COM interop (`WUApiLib`), DPAPI secret storage, capability probing, and REST source client.
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

The presentation layer can reference infrastructure for composition via `AppComposition.cs` (`IHostBuilder` DI). Domain does not reference application, infrastructure, or UI code.

## Bootstrapping & Composition

Application lifecycle and Dependency Injection are managed in `AppComposition.cs` using `Microsoft.Extensions.Hosting` (`IHost` / `IServiceCollection`). Structured logging is handled via **Serilog** configured with rolling file outputs in `%LOCALAPPDATA%\OnlyWinget\logs\` and an in-memory debug sink `AppDiagnosticsSerilogSink`.

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

## Native Interop & Capabilities

Application startup builds the `IHost`, loads the SQLite workspace, checks OS support, probes Windows edition (Home/Pro/Enterprise/IoT), display version, UI culture/language (`CultureInfo.CurrentUICulture`), UAC elevation privileges, probes `winget` COM and CLI capabilities, checks dual PowerShell availability (PowerShell 7 Core `pwsh.exe` and Windows PowerShell 5.1 `powershell.exe` with dynamic fallback), lists sources, and probes Windows Update COM availability (`WUApiLib`) through `ISystemCapabilityService`.

- **WinGet**: `ComWingetPackageService` leverages native COM interfaces (`Microsoft.Management.Deployment`) with `IMemoryCache` TTL caching, falling back to CLI execution (`ProcessWingetCommandRunner`) if COM is unavailable. Operation failures are classified by `WingetErrorClassifier` across all locales using standard HRESULT exit codes (`HashMismatch` `0x8A150002`, `NotFound` `0x8A150014`, `Cancelled` `0x8A150015`/`0x800704C7`, `NoUpdates` `0x8A15002B`, `SourceUnavailable` `0x8A15005E`, `CannotUpgrade` `0x8A150114`-`0x8A150117`). Non-retryable errors like `HashMismatch` fail cleanly with actionable user guidance to enable `InstallerHashOverride` or bypass validation in Settings.
- **Windows Update**: `ComWindowsUpdateService` executes direct C# COM Interop (`IUpdateSession` / `IUpdateSearcher`) with real-time progress callbacks, falling back to dynamic PowerShell Base64 scripts (`PowerShellWindowsUpdateService`) supporting both `pwsh.exe` and `powershell.exe`. Supports optional updates (drivers and preview updates via `BrowseOnly`).
- **Direct Search Operations**: Packages found via search can be installed directly without requiring inclusion in a preset or modifying existing presets.
- **Process Security**: `ProcessExternalProcessRunner` handles process execution asynchronously using `ProcessStartInfo.ArgumentList` (no shell involved), so arguments are passed as discrete process parameters rather than a concatenated command line. The app runs elevated (`app.manifest` requests `requireAdministrator`) with runtime UAC privilege verification.

## Presentation & MVVM

The WinUI shell is route-driven through `Shell/NavigationRegistry.cs`. User-facing routes are Home, Packages, Updates, Sources, Activity, and Settings. All ViewModels utilize **`CommunityToolkit.Mvvm`** (v8.4+) with `[ObservableProperty]` and `[RelayCommand]` source generators, communicating via `WeakReferenceMessenger`.

Reusable presentation primitives live under `DesignSystem`: `PageScaffold` owns page chrome and responsive spacing, `OnlyWingetCommandBar` renders typed `UiCommand` definitions, and `OnlyWingetResponsivePanel`/`OnlyWingetWrapPanel` provide adaptive layout. State controls (`StatePresenter`) provide consistent inline status and error transitions. `OnlyWingetTable` owns shared header/row columns, items virtualization (`ItemsRepeater`), horizontal scrolling, keyboard multi-selection, mixed select-all, UI Automation names, and stable collection binding.

`Controls/OperationTrackerControl` is a persistent top-of-shell banner that shows operation progress and links to the Activity log. It is always visible in `MainWindow` above the page host, inside the `NavigationView`.

Feature ViewModels own operations, cancellation, validation, confirmation, clipboard, settings, and picker orchestration through the UI service collection created in `AppComposition`.


## Installer

The release artifact is an x64 NSIS setup EXE created from a self-contained `win-x64` publish. Packaging also produces a matching self-contained x64 portable ZIP.

## Notes

Keep this file high-level. Put commands in [`operations.md`](operations.md), release steps in [`release.md`](release.md), and open todos in [`../PROJECT_STATUS.json`](../PROJECT_STATUS.json).
