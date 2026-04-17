# Architecture

## Purpose

OnlyWinget is a Windows desktop client for managing `winget` packages from a local WPF UI. The application focuses on curated preset lists, package discovery, update workflows, and batch package operations driven by the system `winget` CLI.

## Repository layout

- `src/OnlyWinget/`: WPF application targeting `net8.0-windows`
- `src/OnlyWinget.Setup/`: WiX source for internal MSIs and the unified setup bundle
- `tests/OnlyWinget.Tests/`: xUnit test project
- `scripts/`: PowerShell entrypoints used for build, verification, cleanup, and unified setup/MSI generation
- `.github/workflows/build-gate.yml`: CI workflow that runs the repository verification gate
- `tools/wix314-binaries/`: bundled WiX 3.14 toolset used by the MSI packaging script
- `artifacts/`: normalized build, test, publish, and packaging output root configured by `Directory.Build.props`

## Solution structure

`OnlyWinget.sln` contains two projects:

- `OnlyWinget`: the desktop application
- `OnlyWinget.Tests`: the test suite

The WiX installer source is versioned under `src/OnlyWinget.Setup`, but it is not a `.csproj` and is packaged through PowerShell rather than the solution build.

## Application architecture

The app uses a lightweight MVVM structure:

- `ViewModels/MainViewModel.cs` orchestrates the shell, search, updates, logging, progress reporting, and batch operations.
- `ViewModels/PresetWorkspaceViewModel.cs` manages preset tabs, CRUD actions, import/export, and local list editing.
- `Services/` contains the application services for persistence, localization, `winget` process execution, package interrogation, updates, tabs, dialogs, and elevation handling.
- `Models/` contains the observable UI models and payload types used by the workspace, update, and interrogation flows.

The repository is intentionally small and does not introduce a separate backend or data service. All state is local to the Windows client.

## Runtime behavior

### Presets and local state

The main preset library is stored at:

- `%LOCALAPPDATA%\OnlyWinget\AppsList.json`

The app supports automatic one-time migration from a legacy `AppsList.json` placed next to the executable.

Preset rows support install, uninstall, and pause actions. Paused rows are skipped during batch execution and marked as paused in the UI instead of invoking `winget`.

UI preferences are stored at:

- `%LOCALAPPDATA%\OnlyWinget\settings.json`

The current implementation persists the preferred UI language there.

### Import and export

Presets can be exported and imported as readable JSON files using the `.onlywinget.json` extension. The import flow normalizes preset names and deduplicates package IDs.

### Localization

The runtime UI currently supports two locales:

- Italian (`it`)
- English (`en`)

`LocalizationService` selects the startup locale from persisted preferences when available, otherwise it falls back to the current Windows UI culture, defaulting to Italian for non-English systems.

The runtime localization catalog applies to the WPF shell. MSI installer UI text is defined separately in WiX and does not follow the application language setting.

### `winget` integration

The application depends on the system `winget` CLI being available. `AppStartupCoordinator` blocks normal startup when `winget` is unavailable and can open the Microsoft App Installer page.

`WingetService` is responsible for:

- checking `winget` availability
- searching packages across the configured `winget` sources and preserving each result source when `winget` reports it
- loading available upgrades
- running install, upgrade, and uninstall commands
- checking and upgrading `Microsoft.AppInstaller`
- decoding `winget` process output as UTF-8 so localized and non-ASCII package names are preserved
- writing per-operation logs under the local runtime directory

Runtime process logs and temporary files are isolated under:

- `%LOCALAPPDATA%\OnlyWinget\runtime`

Runtime cleanup prunes `.log` files older than 30 days. It preserves the runtime directory itself, recent logs, and non-log temporary files.

### Package interrogation

Before a searched package is added to a preset, the app runs a package interrogation flow using the source reported by search, defaulting to `winget` when no source is reported:

1. `winget show` resolves package metadata.
2. For `winget` sources with a concrete version, the app tries to fetch the installer manifest from `microsoft/winget-pkgs`.
3. Installer candidates are normalized into selectable options such as scope, architecture, locale, installer type, and install mode.
4. If manifest data is unavailable, the dialog falls back to a reduced mode instead of blocking the workflow.

## Installer architecture

`src/OnlyWinget.Setup/OnlyWinget.Setup.wxs` defines parameterized x86 and x64 internal MSIs with these observable characteristics:

- `InstallScope="perMachine"`
- major upgrade support through `MajorUpgrade`, including same-version reinstall handling
- install root under `ProgramFilesFolder\OnlyWinget` for x86 and `ProgramFiles64Folder\OnlyWinget` for x64
- a Start Menu shortcut using the application icon
- optional desktop shortcut feature
- uninstall cleanup for the install directory
- a bundled PowerShell cleanup script to remove project-related Windows services during uninstall

The MSIs are built from framework-dependent `dotnet publish` outputs for `win-x86` and `win-x64`; the default packaging flow does not produce self-contained app payloads.

`src/OnlyWinget.Setup/OnlyWinget.Bundle.wxs` defines the primary end-user setup. It is a WiX Burn EXE that embeds both internal MSIs and selects the x64 MSI when `VersionNT64` is true, otherwise selecting the x86 MSI. This avoids presenting separate x86 and x64 installers as the primary distribution workflow while preserving architecture-specific payloads internally.

## Verification boundaries

This document describes repository structure and observable runtime behavior from source, tests, and packaging definitions. It does not assert real install, upgrade, or uninstall execution unless that evidence is tracked separately in repository-owned artifacts or status tracking.
