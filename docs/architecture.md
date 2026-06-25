# Architecture

OnlyWinget is a Windows WPF client for local `winget` package workflows.

## Layout

- `src/OnlyWinget`: WPF app targeting `net8.0-windows`.
- `src/OnlyWinget.Setup`: WiX setup sources.
- `tests/OnlyWinget.Tests`: xUnit tests.
- `scripts`: PowerShell entrypoints.
- `media/Default.onlywinget.json`: sample preset.

## Core Flow

- `MainViewModel` coordinates shell state, workspaces, progress, cancellation, and commands.
- `PresetWorkspaceViewModel`, `SearchWorkspaceViewModel`, and `UpdatesWorkspaceViewModel` own their UI areas.
- `WingetCommandService` is the low-level `winget` command boundary.
- `WingetQueryService` owns search, update loading, package resolution, and installed details.
- `WingetPackageInterrogationService` resolves package metadata before adding search results.
- `PackageOperationService` builds and classifies install, uninstall, upgrade, App Installer update, and source update operations.
- `OperationRunner` adapts operation results to row status, progress, and logs.

## Local State

- Presets: `%LOCALAPPDATA%\OnlyWinget\AppsList.json`
- Preferences: `%LOCALAPPDATA%\OnlyWinget\settings.json`
- Runtime logs: `%LOCALAPPDATA%\OnlyWinget\runtime`

Preset JSON uses schema v2. Row selection is UI-only and is not persisted.

## Installer

The release artifact is a WiX Burn setup EXE. It embeds x86 and x64 self-contained MSI payloads, blocks unsupported Windows versions, and installs per-machine under Program Files.

## Notes

Keep this file high-level. Put commands in [`operations.md`](operations.md), release steps in [`release.md`](release.md), and open todos in [`../PROJECT_STATUS.json`](../PROJECT_STATUS.json).
