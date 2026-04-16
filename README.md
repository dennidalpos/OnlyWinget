# OnlyWinget

<p align="center">
  <img src="src/OnlyWinget/Assets/OnlyWinget-icon.png" alt="OnlyWinget icon" width="128" />
</p>

OnlyWinget is a Windows desktop client for managing packages through the local `winget` CLI. It provides a WPF interface for keeping curated package presets, discovering packages, reviewing installer choices, and running package operations from a local Windows workflow.

## Features

- Create and manage reusable package preset tabs.
- Search packages through the configured `winget` sources.
- Review package metadata and installer options before adding search results to a preset.
- Run batch install, upgrade, and uninstall operations.
- Pause preset rows so they are skipped during a batch run.
- View available upgrades in a dedicated updates workspace.
- Import and export presets as readable `.onlywinget.json` files.
- Persist local app data and UI preferences under `%LOCALAPPDATA%\OnlyWinget`.
- Use the UI in Italian or English.
- Detect missing `winget` support at startup and guide users toward Microsoft App Installer.

## Requirements

- Windows 10 or Windows 11.
- `winget` available on `PATH`.
- .NET SDK compatible with [`global.json`](global.json) for local development.
- PowerShell 7 (`pwsh`) for repository scripts.

## Run From Source

```powershell
dotnet restore .\OnlyWinget.sln --locked-mode
dotnet run --project .\src\OnlyWinget\OnlyWinget.csproj
```

For the repository build, test, and Windows packaging flow:

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\build-gate.ps1 -Configuration Release
```

## Project Status

The repository contains the WPF application, xUnit tests, Windows-first PowerShell build scripts, and WiX packaging source. CI is configured through GitHub Actions on `windows-latest`.

The current application version in the project file is `1.0.2`. The primary package is one setup EXE containing x86 and x64 internal MSIs; clean-machine setup install, reinstall, uninstall, and major-upgrade validation remain tracked as blocked work in [`PROJECT_STATUS.json`](PROJECT_STATUS.json). Treat build, packaging, and release validation as separate steps.

## Documentation

- [Architecture](docs/architecture.md): repository layout, runtime behavior, local state, `winget` integration, and installer architecture.
- [Build, Test, and Delivery](docs/operations.md): canonical PowerShell entrypoints, CI behavior, unified setup/MSI packaging, cleanup, and verification boundaries.
- [Project Status](PROJECT_STATUS.json): current open verification work.

## License

OnlyWinget is proprietary software. Copyright (c) 2026 Danny Perondi. All rights reserved.
