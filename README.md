# OnlyWinget

<p align="center">
  <img src="src/OnlyWinget/Assets/OnlyWinget-icon.png" alt="OnlyWinget" width="128" />
</p>

<p align="center">
  <img alt="Windows 10/11" src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4" />
  <img alt=".NET" src="https://img.shields.io/badge/.NET-10%20WinUI%203-512BD4" />
  <img alt="winget" src="https://img.shields.io/badge/winget-1.x-107C10" />
  <img alt="Tests" src="https://img.shields.io/badge/tests-xUnit-2EA44F" />
  <img alt="Languages" src="https://img.shields.io/badge/UI-English%20%7C%20Italian-555" />
</p>

OnlyWinget is a Windows desktop app for managing local `winget` workflows from a WinUI 3 UI: curated presets, package search, update review, and repeatable install or uninstall batches.

## Highlights

| Area | What it does |
| --- | --- |
| Presets | Create package lists, import/export the current `onlywinget.preset.v1` format, and run selected install/uninstall actions. |
| Search | Search local `winget` sources, resolve package identity, and add selected results in batches. |
| Updates | Review available upgrades, select all or specific rows, and apply selected updates. |
| Safety | Clean architecture boundaries, cancellable operation design, current-format-only import, and local-only workspace storage. |
| Installer | Unified Windows setup EXE with an x64 self-contained MSI payload. |

## Metrics

| Metric | Current value |
| --- | --- |
| App framework | `net10.0-windows10.0.17763.0` WinUI 3 |
| Test suite | xUnit tests under `tests/OnlyWinget.Tests` |
| UI languages | English, Italian |
| Release artifacts | 1 x64 setup EXE, 1 internal x64 MSI, and 1 x64 portable ZIP |
| Local data root | `%LOCALAPPDATA%\OnlyWinget` |

## Requirements

- Windows 10 or Windows 11.
- Microsoft App Installer with `winget` 1.x on `PATH`.
- Windows App Runtime matching the Windows App SDK package used by the app. The setup bundle installs this prerequisite.
- For development: PowerShell 7+. The repository scripts install missing local prerequisites where practical: .NET SDK from [`global.json`](global.json), PSScriptAnalyzer, WiX Toolset 3.x, and the Windows App Runtime redistributable used for bundling.

Quick verification:

```powershell
winver
winget --version
```

## Commands

Run task examples:

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\run.ps1 -Task Setup
pwsh -ExecutionPolicy Bypass -File .\scripts\run.ps1 -Task Format -NoRestore
pwsh -ExecutionPolicy Bypass -File .\scripts\run.ps1 -Task Typecheck -Configuration Release -NoRestore
pwsh -ExecutionPolicy Bypass -File .\scripts\run.ps1 -Task Test -Configuration Release -NoRestore
pwsh -ExecutionPolicy Bypass -File .\scripts\run.ps1 -Task Build -Configuration Release
pwsh -ExecutionPolicy Bypass -File .\scripts\run.ps1 -Task Package -Configuration Release -NoRestore
```

Full local gate:

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\run.ps1 -Task Check -Configuration Release
```

## Project Links

- [Architecture](docs/architecture.md)
- [Operations](docs/operations.md)
- [Release](docs/release.md)
- [Scripts](scripts/README.md)
- [Open todos](PROJECT_STATUS.json)

## License

OnlyWinget is proprietary software. Copyright (c) 2026 Danny Perondi. All rights reserved.
