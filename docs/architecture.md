# Architecture

OnlyWinget is a WinUI 3 desktop client for local `winget` package workflows.

## Layout

- `src/OnlyWinget.Domain`: package identity, presets, batch selection, operation plans, status, and validation primitives.
- `src/OnlyWinget.Application`: use-case services, preset import/export, workspace storage contracts, and winget boundaries.
- `src/OnlyWinget.Infrastructure`: JSON workspace persistence and winget command parsing/building adapters.
- `src/OnlyWinget`: WinUI 3 presentation shell targeting `.NET 10` and Windows 10 build `17763`.
- `src/OnlyWinget.Setup`: WiX setup sources, packaged by `scripts/package.ps1` rather than included as an SDK project in the solution.
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

The workspace schema is:

```text
%LOCALAPPDATA%\OnlyWinget\workspace-v1.json
```

Preset exchange supports only `onlywinget.preset.v1`.

No legacy sample preset is shipped with the repository. Presets are created in the app or imported explicitly in the current exchange format.

## Capabilities

Application startup checks OS support, `winget`, PowerShell, and Windows Update COM availability through one capability service. Presentation commands are disabled when a required capability is unavailable, and infrastructure services return structured failures instead of invoking unsupported APIs.

## Presentation

The WinUI shell is route-driven through `Shell/NavigationRegistry.cs`. User-facing routes are Home, Packages, Updates, Sources, Activity, and Settings; Packages and Updates own their provider-specific modes instead of exposing technical modules in primary navigation.

Reusable presentation primitives live under `DesignSystem`: `PageScaffold` owns page chrome and responsive spacing, `OnlyWingetCommandBar` renders typed `UiCommand` definitions, and the state controls provide consistent inline status and operation progress. Feature pages live under `Features`; older page implementations are removed as each feature migration completes rather than retained through compatibility adapters.

## Installer

The release artifact is an x64 WiX Burn setup EXE with one x64 MSI payload. The bundle chains the x64 Windows App Runtime redistributable before installing the WinUI app MSI. Packaging also produces a self-contained x64 portable ZIP.

## Notes

Keep this file high-level. Put commands in [`operations.md`](operations.md), release steps in [`release.md`](release.md), and open todos in [`../PROJECT_STATUS.json`](../PROJECT_STATUS.json).
