# OnlyWinget

<p align="center">
  <img src="src/OnlyWinget/Assets/OnlyWinget-icon.png" alt="OnlyWinget icon" width="128" />
</p>

OnlyWinget is a Windows WPF desktop app for managing packages with the local `winget` CLI. It is focused on curated preset lists, package discovery, update review, and repeatable local package operations from a desktop UI.

## Verified Features

- Create, rename, delete, import, and export preset tabs for package lists.
- Search packages from available `winget` sources and inspect package metadata before adding entries.
- Run install, uninstall, and pause actions from saved presets.
- Review available upgrades in a dedicated updates workspace and apply selected updates.
- Persist local presets, runtime state, and UI language preferences under `%LOCALAPPDATA%\OnlyWinget`.
- Use the app in English or Italian.

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

## Current Project Status

- The repository contains the desktop app, automated tests, PowerShell build entrypoints, and WiX-based Windows packaging sources.
- Local CI-equivalent verification is versioned in `scripts/build-gate.ps1` and mirrored by GitHub Actions.
- Windows setup packaging is versioned and builds a unified setup EXE plus internal x86/x64 MSIs.
- One blocked packaging validation item remains tracked in [PROJECT_STATUS.json](PROJECT_STATUS.json): verifying a real major upgrade from a supported previous release on a clean or dedicated Windows host.

## Technical Documentation

- [Architecture](docs/architecture.md)
- [Build, Test, and Delivery](docs/operations.md)
- [Project Status](PROJECT_STATUS.json)

## License

OnlyWinget is proprietary software. Copyright (c) 2026 Danny Perondi. All rights reserved.
