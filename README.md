# OnlyWinget

<p align="center">
  <img src="src/OnlyWinget/Assets/OnlyWinget-icon.png" alt="OnlyWinget icon" width="128" />
</p>

OnlyWinget is a Windows desktop app for managing local `winget` package workflows from a WPF UI. It is built for curated preset lists, package discovery, update review, and repeatable install or uninstall operations on a Windows machine.

## Verified Features

- Create, rename, delete, import, and export package preset tabs.
- Search packages through the local `winget` sources and preserve the reported source.
- Inspect package metadata before adding searched packages to a preset.
- Configure install options captured from package metadata, including version, scope, architecture, locale, installer type, install mode, and custom arguments when available.
- Run install, uninstall, and pause actions from saved presets.
- Review available package upgrades and run selected upgrades.
- Cancel an in-progress batch or update operation; direct and elevated `winget` executions are bounded by timeouts.
- Persist presets, runtime logs, and UI preferences under `%LOCALAPPDATA%\OnlyWinget`.
- Use the runtime UI in English or Italian.
- Build a Windows setup EXE that embeds x86 and x64 MSI payloads.

## Requirements

- Windows 10 or Windows 11.
- `winget` available on `PATH` for normal app use.
- .NET SDK compatible with [`global.json`](global.json).
- PowerShell 7+ (`pwsh`) for repository scripts.
- WiX 3.14 is already bundled under `tools\wix314-binaries` for packaging.

The generated setup is self-contained and does not require the .NET Desktop Runtime to be installed on end-user machines.

## Setup

Fresh repository setup restores NuGet dependencies in locked mode:

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\install.ps1
```

Equivalent direct restore:

```powershell
dotnet restore .\OnlyWinget.sln --locked-mode
```

## Main Commands

```powershell
# Verify formatting
dotnet format .\OnlyWinget.sln --verify-no-changes --no-restore

# Build with warnings as errors
pwsh -ExecutionPolicy Bypass -File .\scripts\build.ps1 -Configuration Release -WarnAsError -NoRestore

# Build the app
pwsh -ExecutionPolicy Bypass -File .\scripts\build.ps1 -Configuration Release

# Run the built app
pwsh -ExecutionPolicy Bypass -File .\scripts\run.ps1 -Configuration Release

# Run unit tests
dotnet test .\tests\OnlyWinget.Tests\OnlyWinget.Tests.csproj -c Release --no-restore --results-directory .\artifacts\test-results --logger "trx;LogFileName=unit-tests.trx"

# Build setup EXE and internal self-contained MSIs
pwsh -ExecutionPolicy Bypass -File .\scripts\package.ps1 -Configuration Release -NoRestore

# Validate real setup install, upgrade, repair, and uninstall lifecycle
pwsh -ExecutionPolicy Bypass -File .\scripts\validate-installer-lifecycle.ps1 -Configuration Release -NoRestore

# Reproduce the CI verification gate locally
pwsh -ExecutionPolicy Bypass -File .\scripts\internal\build-gate.ps1 -Configuration Release
```

Typecheck is covered by C# compilation in the build and build-gate scripts. The default gate restores, verifies formatting, builds with warnings as errors, runs tests, builds the app, generates the setup packages, and writes `artifacts\build-report.txt`.

## Project Status

The repository contains the WPF app, xUnit tests, Windows PowerShell entrypoints, WiX packaging sources, bundled WiX 3.14 binaries, and a GitHub Actions build gate. Source build, unit/UI tests, package generation, and CI verification are automated.

Live `winget` smoke tests are opt-in through `-RunWingetSmoke`. Installer lifecycle validation requires an elevated clean or dedicated Windows host and writes its report under `artifacts\installer-validation\`.

## Technical Documentation

- [Architecture](docs/architecture.md)
- [Build, Test, and Delivery](docs/operations.md)
- [Project Status](PROJECT_STATUS.json)

## License

OnlyWinget is proprietary software. Copyright (c) 2026 Danny Perondi. All rights reserved.
