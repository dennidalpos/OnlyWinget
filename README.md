# OnlyWinget

<p align="center">
  <img src="src/OnlyWinget/Assets/OnlyWinget-icon.png" alt="OnlyWinget icon" width="128" />
</p>

OnlyWinget is a Windows desktop client for managing packages through the local `winget` CLI. It provides a WPF interface for curated package presets, package search, update review, and batch package operations on Windows.

## Verified Feature Set

- Manage reusable preset tabs for package lists.
- Search packages from configured `winget` sources.
- Review package metadata and installer options before adding a package to a preset.
- Run batch install, upgrade, and uninstall operations.
- Pause preset rows so they are skipped during batch execution.
- Review available upgrades in a dedicated updates workspace.
- Import and export presets as readable `.onlywinget.json` files.
- Persist app data and UI preferences under `%LOCALAPPDATA%\OnlyWinget`.
- Use the UI in Italian or English.

## Windows-First Local Setup

Requirements:

- Windows 10 or Windows 11
- `winget` available on `PATH`
- .NET SDK compatible with [global.json](global.json)
- PowerShell 7 (`pwsh`) for repository scripts

Run from source:

```powershell
dotnet restore .\OnlyWinget.sln --locked-mode
dotnet run --project .\src\OnlyWinget\OnlyWinget.csproj
```

Canonical repository verification:

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\build-gate.ps1 -Configuration Release
```

## Project Status

- The repository contains the WPF application, xUnit tests, Windows-first PowerShell build scripts, and WiX packaging source.
- CI is configured through GitHub Actions on `windows-latest`.
- The current project version is `1.0.2`.
- Local install, same-version rerun, launch, and uninstall validation of the generated setup have been executed on an elevated x64 Windows host.
- One packaging validation item remains open in [PROJECT_STATUS.json](PROJECT_STATUS.json): major-upgrade verification from a supported previous version.

## Technical Documentation

- [Architecture](docs/architecture.md)
- [Build, Test, and Delivery](docs/operations.md)
- [Project Status](PROJECT_STATUS.json)

## License

OnlyWinget is proprietary software. Copyright (c) 2026 Danny Perondi. All rights reserved.
