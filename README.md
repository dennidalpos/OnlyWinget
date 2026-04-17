# OnlyWinget

<p align="center">
  <img src="src/OnlyWinget/Assets/OnlyWinget-icon.png" alt="OnlyWinget icon" width="128" />
</p>

OnlyWinget is a Windows WPF desktop client for managing packages through the local `winget` CLI. The repository contains the application, xUnit coverage for core behaviors, PowerShell build entrypoints, and WiX-based Windows packaging sources.

## Overview

- Build and manage reusable preset tabs for package lists.
- Search packages from available `winget` sources and inspect package metadata before adding them.
- Run install, upgrade, and uninstall operations from the app.
- Review available updates in a dedicated workspace.
- Import and export presets as `.onlywinget.json` files.
- Persist local app data and UI preferences under `%LOCALAPPDATA%\OnlyWinget`.
- Use the UI in Italian or English.

## Windows-First Setup

Requirements:

- Windows 10 or Windows 11
- `winget` available on `PATH`
- .NET SDK compatible with [global.json](global.json)
- PowerShell 7 (`pwsh`) for repository scripts

Canonical repository verification:

```powershell
dotnet restore .\OnlyWinget.sln --locked-mode
pwsh -ExecutionPolicy Bypass -File .\scripts\build-gate.ps1 -Configuration Release
```

## Project Status

- The repository contains the WPF application, xUnit tests, Windows-first PowerShell build scripts, and WiX packaging source.
- CI is configured through GitHub Actions on `windows-latest`.
- The current project version is `1.0.2`.
- Current build artifacts in `artifacts/` show that the app build, test run, and setup generation have been executed locally.
- One repository-evidenced packaging validation item remains open in [PROJECT_STATUS.json](PROJECT_STATUS.json): major-upgrade verification from a supported previous release artifact on a clean or dedicated Windows host.

## Technical Documentation

- [Architecture](docs/architecture.md)
- [Build, Test, and Delivery](docs/operations.md)
- [Project Status](PROJECT_STATUS.json)

## License

OnlyWinget is proprietary software. Copyright (c) 2026 Danny Perondi. All rights reserved.
