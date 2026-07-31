# Architecture

OnlyWinget is a WinUI 3 desktop client for local `winget` package workflows and explicit Windows Update scans.

## Layout

- `src/OnlyWinget.Domain`: package identity, presets, batch selection, operation plans, status, and validation primitives.
- `src/OnlyWinget.Application`: use-case orchestration, preset import/export, workspace/source-preference storage contracts, capability contracts, and `winget`/Windows Update ports.
- `src/OnlyWinget.Infrastructure`: JSON workspace/source-preference persistence, process execution, capability probing, `winget` command adapters, and Windows Update PowerShell integration.
- `src/OnlyWinget`: WinUI 3 presentation shell targeting `.NET 10` and Windows 10 build `17763`.
- `src/OnlyWinget.Setup`: NSIS setup script and assets, packaged by `scripts/package.ps1`.
- `tests/OnlyWinget.Tests`: xUnit tests for non-UI behavior.
- `scripts`: PowerShell entrypoints.

## Dependency Rule

Dependencies point inward:

```text
WinUI Presentation -> Application -> Domain
Infrastructure -----> Application -> Domain
```

The presentation layer can reference infrastructure for composition. Domain does not reference application, infrastructure, or UI code.

## Local State

The local JSON files are:

```text
%LOCALAPPDATA%\OnlyWinget\workspace-v1.json
%LOCALAPPDATA%\OnlyWinget\source-preferences-v1.json
%LOCALAPPDATA%\OnlyWinget\settings.json
```

Preset exchange supports only `onlywinget.preset.v1`.

## Capabilities

Application startup loads the workspace, checks OS support, checks `winget`, lists sources, and probes PowerShell plus Windows Update COM availability through `ISystemCapabilityService`. Presentation commands are disabled when a required capability is unavailable, and infrastructure services return structured failures instead of invoking unsupported APIs.

## Presentation

The WinUI shell is route-driven through `Shell/NavigationRegistry.cs`. User-facing routes are Home, Packages, Updates, Sources, Activity, and Settings; Packages and Updates own their provider-specific modes instead of exposing technical modules in primary navigation.

Reusable presentation primitives live under `DesignSystem`: `PageScaffold` owns page chrome and responsive spacing, `OnlyWingetCommandBar` renders typed `UiCommand` definitions, and `OnlyWingetResponsivePanel`/`OnlyWingetWrapPanel` provide adaptive layout. State controls (`StatePresenter`) provide consistent inline status and error transitions. `OnlyWingetTable` owns shared header/row columns, horizontal scrolling, keyboard multi-selection, mixed select-all, UI Automation names, and stable collection binding.

`Controls/OperationTrackerControl` is a persistent top-of-shell banner that shows operation progress and links to the Activity log. It is always visible in `MainWindow` above the page host, inside the `NavigationView`.

Feature ViewModels own operations, cancellation, validation, confirmation, clipboard, settings, and picker orchestration through the UI service collection created in `AppComposition`.


## Installer

The release artifact is an x64 NSIS setup EXE created from a self-contained `win-x64` publish. Packaging also produces a matching self-contained x64 portable ZIP.

## Notes

Keep this file high-level. Put commands in [`operations.md`](operations.md), release steps in [`release.md`](release.md), and open todos in [`../PROJECT_STATUS.json`](../PROJECT_STATUS.json).
