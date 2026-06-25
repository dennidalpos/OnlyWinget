# Architecture

## Documentation map

This document covers structure and runtime behavior.

Use the adjacent repository documents for the other concerns:

- [`../README.md`](../README.md): product-facing overview
- [`operations.md`](operations.md): setup, canonical commands, CI reproduction, and troubleshooting
- [`../PROJECT_STATUS.json`](../PROJECT_STATUS.json): current incomplete and actionable repository todos when present

## Purpose

OnlyWinget is a Windows desktop client for managing `winget` packages from a local WPF UI. The application focuses on curated preset lists, package discovery, update workflows, and batch package operations driven by the system `winget` CLI.

## Repository layout

- `src/OnlyWinget/`: WPF application targeting `net8.0-windows`
- `src/OnlyWinget.Setup/`: WiX source for internal MSIs and the unified setup bundle
- `tests/OnlyWinget.Tests/`: xUnit test project
- `scripts/`: PowerShell entrypoints used for install, build, run, cleanup, verification, and unified setup/MSI generation
- `.github/workflows/build-gate.yml`: CI workflow that runs the repository verification gate through `scripts/check.ps1`
- `tools/`: repository-owned tooling inputs; `tools/wix314-binaries/` is an optional local WiX toolset location when present
- `media/Default.onlywinget.json`: sample preset JSON file
- `artifacts/`: normalized build, test, publish, and packaging output root configured by `Directory.Build.props`

## Solution structure

`OnlyWinget.sln` contains two projects:

- `OnlyWinget`: the desktop application
- `OnlyWinget.Tests`: the test suite

The WiX installer source is versioned under `src/OnlyWinget.Setup`, but it is not a `.csproj` and is packaged through PowerShell rather than the solution build.

## Application architecture

The app uses a lightweight MVVM structure:

- `ViewModels/MainViewModel.cs` orchestrates shell state, high-level commands, progress, cancellation, and cross-workspace operations.
- `ViewModels/PresetWorkspaceViewModel.cs` manages preset tabs, CRUD actions, import/export, local list editing, selected-row counts, tri-state preset selection, and selected-row batch actions.
- `ViewModels/SearchWorkspaceViewModel.cs` owns search query state, search result selection, selected counts, and tri-state search result selection.
- `ViewModels/UpdatesWorkspaceViewModel.cs` owns update list state, update selection, selected counts, and tri-state update selection.
- `Services/OutputLogBuffer.cs` stores bounded operation output and keeps the shell log from growing without limit.
- `Services/` contains the application services for persistence, localization, `winget` process execution, package interrogation, package operation orchestration, updates, tabs, dialogs, and elevation handling.
- `Models/` contains the observable UI models and payload types used by the workspace, update, and interrogation flows.

The repository is intentionally small and does not introduce a separate backend or data service. All state is local to the Windows client.

## Runtime behavior

### Presets and local state

The main preset library is stored at:

- `%LOCALAPPDATA%\OnlyWinget\AppsList.json`

The app supports only schema v2 JSON under `%LOCALAPPDATA%\OnlyWinget`. The local preset library has this shape:

```json
{
  "SchemaVersion": 2,
  "Tabs": [
    {
      "Name": "Default",
      "Apps": []
    }
  ]
}
```

The local preset library and imported preset JSON files are rejected before full deserialization when they exceed the current 5 MiB application data limit.

If the saved preset library is invalid, corrupted, legacy, or temporarily unreadable, the UI starts with an empty default preset and marks the original file as requiring recovery protection. Before the app writes a replacement `AppsList.json`, it first copies the original file to a timestamped `AppsList.json.recovery-*.bak` file in the same directory. If that recovery backup cannot be created, the save is blocked.

Preset rows support install, uninstall, and pause actions. Row checkbox selection is transient UI state and is not saved. Batch apply runs only selected rows whose action is not pause. Paused rows are the only persisted way to keep a row but skip it during execution.

UI preferences are stored at:

- `%LOCALAPPDATA%\OnlyWinget\settings.json`

The current implementation persists the preferred UI language there. Settings JSON is rejected before full deserialization when it exceeds the current 1 MiB settings limit.

### Import and export

Presets can be exported and imported as readable schema v2 JSON files using the `.onlywinget.json` extension. Exported presets preserve package action, source, installer selectors, install mode, log/location support, elevation metadata, and advanced installer arguments. Exported presets do not include row selection state. The import flow normalizes preset names and deduplicates exact package entries by package ID, source, and architecture.

Imported preset rows that contain advanced installer arguments (`--custom` or `--override`) are treated as untrusted until reviewed in the package options dialog, even when the file marks them as previously reviewed. Batch execution blocks those rows and does not pass their advanced arguments to `winget` until the user reviews and saves the row.

Advanced installer arguments are persisted in `AppsList.json` and included in exported preset files, so users should not type tokens, license keys, passwords, or other secrets directly into those fields. The package options dialog warns about this storage model and redacts `--custom` and `--override` values in the command preview. When a secret-like value is required, the supported pattern is an environment-variable placeholder such as `%ONLYWINGET_LICENSE_KEY%`; the placeholder is saved in the preset and expanded only while building the final `winget install` command.

### Localization

The runtime UI currently supports two locales:

- Italian (`it`)
- English (`en`)

`LocalizationService` selects the startup locale from persisted preferences when available, otherwise it falls back to the current Windows UI culture, defaulting to Italian for non-English systems.

The runtime localization catalog applies to the WPF shell. MSI installer UI text is defined separately in WiX and does not follow the application language setting.

### `winget` integration

The application depends on the system `winget` CLI being available. `AppStartupCoordinator` blocks before the main window is shown when `winget` is unavailable, explains that Microsoft App Installer with `winget` 1.x is required, gives `winget --version` as the verification command, and can open the official Microsoft App Installer page. Non-blocking post-startup checks log unexpected failures as structured `startup_check_failed` events with the failing stage, exception type, and HResult, without copying raw exception messages into the user-facing log.

`WingetService` remains the compatibility facade used by existing callers. Internally, query parsing and operation execution stay separated through `WingetTableParser`, `WingetOutputClassifier`, `WingetPackageInterrogationService`, `PackageOperationService`, and `OperationRunner`.

`WingetService` is responsible for:

- checking `winget` availability
- searching packages across the configured `winget` sources and preserving each result source when `winget` reports it
- loading available upgrades
- running install, upgrade, and uninstall commands
- checking and upgrading `Microsoft.AppInstaller`
- decoding `winget` process output as UTF-8 so localized and non-ASCII package names are preserved
- writing per-operation logs under the local runtime directory

`PackageOperationService` is responsible for turning install, uninstall, upgrade, App Installer update, and source-update requests into a single operation pipeline: validate advanced argument review, resolve saved packages, build command arguments, decide direct versus elevated execution, invoke `winget`, classify results, and return a normalized operation result. `OperationRunner` adapts those normalized results to UI row status, progress, and log output.

Batch install, upgrade, and uninstall flows pass a cancellation token from the shell UI to `OperationRunner`, direct `winget` process execution, and elevated launch handling. Direct `winget` calls use command-specific timeouts: short query timeouts for `show`, `search`, and `list`; a medium timeout for source maintenance; and longer bounded timeouts for install, upgrade, and uninstall operations. Elevated launches are also bounded and return local cancellation or timeout result codes when the prompt or child process does not complete.

Runtime process logs and temporary files are isolated under:

- `%LOCALAPPDATA%\OnlyWinget\runtime`

Runtime cleanup prunes `.log` files older than 30 days. It preserves the runtime directory itself, recent logs, and non-log temporary files.

### Package interrogation

Before a searched package is added to a preset, the app runs a package interrogation flow using the source reported by search, defaulting to `winget` when no source is reported:

1. `winget show` resolves package metadata.
2. For `winget` sources with a concrete version and safe package/version path segments, the app tries to fetch the installer manifest from `microsoft/winget-pkgs` with a bounded timeout, cancellation, transient retry, response-size limit, and in-memory cache.
3. Installer candidates are normalized into selectable options such as scope, architecture, locale, installer type, and install mode.
4. If manifest data is unavailable, the dialog falls back to a reduced mode instead of blocking the workflow.

The manifest URL builder accepts only non-empty path segments made of letters, digits, `.`, `-`, `_`, or `+`; unsupported metadata skips the external manifest fetch and degrades to reduced mode. The interrogation parser accepts the standard English and Italian `winget show` package header and also falls back to the stable `Package Name [Package.Id]` shape when `winget` localizes the leading word. Installer manifests are parsed conservatively for the fields the UI uses, including quoted YAML scalars, inline comments, indented installer entries, and quoted inline sequences. Rejected, failed, oversized, unavailable, or timed-out manifest fetches degrade to reduced mode without blocking the package workflow.

### Logging and diagnostics

Operation output is user-visible and intentionally focused on actionable `winget` status. Structured log values produced by package interrogation and elevated launch handling are quoted after control-character and newline sanitization so package metadata or exception text cannot forge additional log events.

## Installer architecture

`src/OnlyWinget.Setup/OnlyWinget.Setup.wxs` defines parameterized x86 and x64 internal MSIs with these observable characteristics:

- `InstallScope="perMachine"`
- a direct-execution launch condition requiring Windows 10 build 10240 or newer
- major upgrade support through `MajorUpgrade`, including same-version reinstall handling
- install root under `ProgramFilesFolder\OnlyWinget` for x86 and `ProgramFiles64Folder\OnlyWinget` for x64
- a Start Menu shortcut using the application icon
- optional desktop shortcut feature
- uninstall cleanup for MSI-tracked files and empty installer-owned directories

The MSIs are built from self-contained `dotnet publish` outputs for `win-x86` and `win-x64`; the default packaging flow embeds the .NET desktop runtime in the setup payload.

`src/OnlyWinget.Setup/OnlyWinget.Bundle.wxs` defines the primary end-user setup. It is a WiX Burn EXE that blocks unsupported Windows versions before the MSI chain runs, embeds both internal MSIs, and selects the x64 MSI when `VersionNT64` is true, otherwise selecting the x86 MSI. This avoids presenting separate x86 and x64 installers as the primary distribution workflow while preserving architecture-specific payloads internally.

## Verification boundaries

This document describes repository structure and observable runtime behavior from source, tests, and packaging definitions. It does not assert real install, upgrade, or uninstall execution unless that evidence is tracked separately in repository-owned artifacts.
