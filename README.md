# OnlyWinget

<p align="center">
  <img src="src/OnlyWinget/Assets/OnlyWinget-icon.png" alt="OnlyWinget icon" width="128" />
</p>

OnlyWinget is a Windows desktop app for managing local `winget` package workflows from a WPF UI. It is built for curated preset lists, package discovery, update review, and repeatable install or uninstall operations on a Windows machine.

## Features

- Create, rename, delete, import, and export package preset tabs.
- Search packages through the local `winget` sources and preserve the reported source.
- Inspect package metadata before adding searched packages to a preset.
- Configure install options captured from package metadata, including version, scope, architecture, locale, installer type, install mode, and custom arguments when available.
- Advanced `--custom` and `--override` arguments are preview-redacted and can use environment-variable placeholders such as `%ONLYWINGET_LICENSE_KEY%` to avoid saving secrets directly in preset files.
- Run install, uninstall, and pause actions from saved presets.
- Review available package upgrades and run selected upgrades.
- Cancel an in-progress batch or update operation; direct and elevated `winget` executions are bounded by timeouts.
- Enforce a single application instance per Windows session to protect the saved preset library from concurrent edits.
- Persist presets, runtime logs, and UI preferences under `%LOCALAPPDATA%\OnlyWinget`.
- Use the runtime UI in English or Italian.
- Build a Windows setup EXE that embeds x86 and x64 MSI payloads.

## Requirements

### End-user runtime

- Windows 10 or Windows 11. The setup blocks unsupported Windows versions before installing payloads.
- Microsoft App Installer with `winget` 1.x available on `PATH` for normal app use. If it is missing, OnlyWinget blocks startup with repair instructions before showing the main window.

The generated setup is self-contained and does not require the .NET Desktop Runtime or Visual C++ Redistributable to be installed on end-user machines.

End-user prerequisite verification:

```powershell
winver
winget --version
```

### Developer toolchain

- .NET SDK compatible with [`global.json`](global.json). The repository currently pins SDK `9.0.100` with `rollForward` set to `latestFeature`.
- PowerShell 7+ (`pwsh`) for repository scripts.
- Optional: `PSScriptAnalyzer` for PowerShell script linting.
- WiX Toolset 3.x for packaging. `scripts\package.ps1` resolves WiX from `tools\wix314-binaries`, `ONLYWINGET_WIX_BIN`, the `WIX` environment variable, standard Program Files locations, or `PATH`.

NuGet restore uses locked mode through `packages.lock.json` files and `Directory.Build.props`.

## Fresh Setup

Fresh repository setup restores NuGet dependencies in locked mode:

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\setup.ps1
```

Equivalent direct restore:

```powershell
dotnet restore .\OnlyWinget.sln --locked-mode
```

Recommended first verification on a fresh workstation:

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\format.ps1 -NoRestore
pwsh -ExecutionPolicy Bypass -File .\scripts\typecheck.ps1 -Configuration Release -NoRestore
pwsh -ExecutionPolicy Bypass -File .\scripts\test.ps1 -Configuration Release -NoRestore
```

## Main Commands

```powershell
# Verify formatting
pwsh -ExecutionPolicy Bypass -File .\scripts\format.ps1 -NoRestore

# Build with warnings as errors
pwsh -ExecutionPolicy Bypass -File .\scripts\typecheck.ps1 -Configuration Release -NoRestore

# Lint PowerShell scripts when PSScriptAnalyzer is installed
pwsh -ExecutionPolicy Bypass -File .\scripts\lint.ps1

# Build the app
pwsh -ExecutionPolicy Bypass -File .\scripts\build.ps1 -Configuration Release

# Run the built app
pwsh -ExecutionPolicy Bypass -File .\scripts\dev.ps1 -Configuration Release

# Run unit tests
pwsh -ExecutionPolicy Bypass -File .\scripts\test.ps1 -Configuration Release -NoRestore

# Clean repository outputs and generated temp files
pwsh -ExecutionPolicy Bypass -File .\scripts\clean.ps1 -Configuration Release

# Extended clean, including Visual Studio/package folders and NuGet/.NET caches
pwsh -ExecutionPolicy Bypass -File .\scripts\clean.ps1 -Configuration Release -All

# Build setup EXE and internal self-contained MSIs
pwsh -ExecutionPolicy Bypass -File .\scripts\package.ps1 -Configuration Release -NoRestore

# Validate real setup install, upgrade, repair, and uninstall lifecycle
pwsh -ExecutionPolicy Bypass -File .\scripts\validate-installer-lifecycle.ps1 -Configuration Release -NoRestore

# Reproduce the CI verification gate locally
pwsh -ExecutionPolicy Bypass -File .\scripts\check.ps1 -Configuration Release
```

Typecheck is covered by C# compilation in the build and check scripts. The default check restores, verifies formatting, lints PowerShell scripts when PSScriptAnalyzer is installed, builds with warnings as errors, runs tests, rebuilds the app, generates the setup packages, and writes `artifacts\build-report.txt`.

There is no versioned deploy or release publishing command in this repository. Release preparation currently stops at build, tests, packaging, and installer lifecycle validation.

## Assets And Samples

- Application icon and package logo: `src\OnlyWinget\Assets\OnlyWinget.ico` and `src\OnlyWinget\Assets\OnlyWinget-icon.png`.
- Installer UI assets: `src\OnlyWinget.Setup\Assets\WixUIBanner.bmp`, `src\OnlyWinget.Setup\Assets\WixUIDialog.bmp`, `src\OnlyWinget.Setup\BurnResponsiveTheme.xml`, and `src\OnlyWinget.Setup\BurnResponsiveTheme.wxl`.
- Sample preset files: `media\Default.onlywinget.json` and `tools\Default.onlywinget.json`.

## Project Status

The repository contains the WPF app, xUnit tests, Windows PowerShell entrypoints, WiX packaging sources, and a GitHub Actions build gate. Source build, unit tests, package generation, and CI verification are scripted.

Live `winget` smoke tests are opt-in through `-RunWingetSmoke`; when disabled, the test report marks them skipped and the build report records `SmokeTests: not_run`. Installer lifecycle validation requires an elevated clean or dedicated Windows host and writes its report under `artifacts\installer-validation\`.

Current operational state, quality gates, residual risks, and handoff notes are tracked in [`PROJECT_STATUS.json`](PROJECT_STATUS.json).

## Troubleshooting

- If `winget --version` fails, install or repair Microsoft App Installer from Microsoft Store before using the app.
- If build, clean, or package output files are locked, close `OnlyWinget.exe` or rerun supported scripts with `-StopRunningInstance`.
- If `scripts\package.ps1` cannot find WiX, install WiX Toolset 3.x, set `ONLYWINGET_WIX_BIN`, set `WIX`, add WiX to `PATH`, or place WiX binaries under `tools\wix314-binaries`.
- Do not put secrets directly in `--custom` or `--override` values. Use environment-variable placeholders such as `%ONLYWINGET_LICENSE_KEY%`; exported preset JSON files include advanced argument text.

## Technical Documentation

- [Architecture](docs/architecture.md)
- [Build, Test, and Delivery](docs/operations.md)
- [Script Inventory](scripts/README.md)
- [Project Status](PROJECT_STATUS.json)

## License

OnlyWinget is proprietary software. Copyright (c) 2026 Danny Perondi. All rights reserved.
