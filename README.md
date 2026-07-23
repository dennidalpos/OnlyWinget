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

OnlyWinget is a Windows desktop app for managing local update workflows from a WinUI 3 UI: curated `winget` presets, package search, source control, update review, Windows Update scans, and repeatable install or uninstall batches.

## Highlights

| Area | What it does |
| --- | --- |
| Presets | Create package lists, import/export the current `onlywinget.preset.v1` format, and run selected install/uninstall actions. |
| Search | Search local `winget` sources, resolve package identity, and add selected results in batches. |
| Updates | Review available `winget` upgrades and Windows Update results, select all or specific rows, and apply selected updates. |
| Sources | Inspect `winget` sources, update/reset source metadata, add/remove sources, and persist disabled-source preferences locally. |
| Safety | Clean architecture boundaries, cancellable operation design, current-format-only preset import, and local-only JSON storage. |
| Installer | Unified x64 NSIS setup EXE and self-contained portable ZIP. |

## Metrics

| Metric | Current value |
| --- | --- |
| App framework | `net10.0-windows10.0.17763.0` WinUI 3 |
| Test suite | xUnit tests under `tests/OnlyWinget.Tests` |
| UI languages | English, Italian |
| Release artifacts | 1 x64 NSIS setup EXE and 1 x64 self-contained portable ZIP |
| Local data root | `%LOCALAPPDATA%\OnlyWinget` (`workspace-v1.json`, `source-preferences-v1.json`, `settings.json`) |

## Requirements

- Windows 10 or Windows 11.
- Microsoft App Installer with `winget` 1.x on `PATH`.
- For development: PowerShell 7+. The repository scripts install missing local prerequisites where practical: .NET SDK from [`global.json`](global.json), PSScriptAnalyzer, and NSIS 3.x for setup creation.

Quick verification:

```powershell
winver
winget --version
```

## Commands

Run task examples:

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\run.ps1 -Task Setup -NonInteractive
pwsh -ExecutionPolicy Bypass -File .\scripts\run.ps1 -Task Format -NoRestore -NonInteractive
pwsh -ExecutionPolicy Bypass -File .\scripts\run.ps1 -Task Typecheck -Configuration Release -NoRestore -NonInteractive
pwsh -ExecutionPolicy Bypass -File .\scripts\run.ps1 -Task Test -Configuration Release -NoRestore -NonInteractive
pwsh -ExecutionPolicy Bypass -File .\scripts\run.ps1 -Task Build -Configuration Release -NonInteractive
pwsh -ExecutionPolicy Bypass -File .\scripts\run.ps1 -Task Package -Configuration Release -NoRestore -NonInteractive
```

Full local gate:

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\run.ps1 -Task Check -Configuration Release -NonInteractive
```

## Project Links

- [Architecture](docs/architecture.md)
- [Operations](docs/operations.md)
- [Release](docs/release.md)
- [Scripts](scripts/README.md)
- [Open todos](PROJECT_STATUS.json)

## License

OnlyWinget is proprietary software. Copyright (c) 2026 Danny Perondi. All rights reserved.
