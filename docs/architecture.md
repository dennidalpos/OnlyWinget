# Architecture

OnlyWinget is a WinUI 3 desktop client for local `winget` package workflows.

## Layout

- `src/OnlyWinget.Domain`: package identity, presets, batch selection, operation plans, status, and validation primitives.
- `src/OnlyWinget.Application`: use-case services, preset import/export, workspace storage contracts, and winget boundaries.
- `src/OnlyWinget.Infrastructure`: JSON workspace persistence and winget command parsing/building adapters.
- `src/OnlyWinget`: WinUI 3 presentation shell targeting `.NET 10` and Windows 10 build `17763`.
- `src/OnlyWinget.Setup`: WiX setup sources.
- `tests/OnlyWinget.Tests`: xUnit tests for non-UI behavior.
- `scripts`: PowerShell entrypoints.
- `media/Default.onlywinget.json`: sample preset.

## Dependency Rule

Dependencies point inward:

```text
WinUI Presentation -> Application -> Domain
Infrastructure -----> Application -> Domain
```

The presentation layer can reference infrastructure for composition. Domain does not reference application, infrastructure, or UI code.

## Local State

The current workspace schema is new for the WinUI modernization:

```text
%LOCALAPPDATA%\OnlyWinget\workspace-v1.json
```

There is no automatic migration from WPF-era files. Preset exchange supports only `onlywinget.preset.v1`.

## Installer

The release artifact remains a WiX Burn setup EXE with x86 and x64 MSI payloads. The WinUI app uses the stable Windows App SDK package and the installer backlog tracks adding the matching Windows App Runtime bootstrap/prerequisite.

## Notes

Keep this file high-level. Put commands in [`operations.md`](operations.md), release steps in [`release.md`](release.md), and open todos in [`../PROJECT_STATUS.json`](../PROJECT_STATUS.json).
