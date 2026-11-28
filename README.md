# OnlyWinget

<p align="center">
  <img src="src/OnlyWinget/Assets/OnlyWinget-icon.png" alt="OnlyWinget logo" width="128" />
</p>

Windows desktop app (.NET 10, WinUI 3) to manage reusable application presets, search WinGet and install WinGet and Windows updates from one interface.

## Purpose

Keep a reusable list of applications, install or update them in one pass, and review what happened. Presets use the exchange format `onlywinget.preset.v1`; the interface is in English and Italian.

## Requirements

- Windows 10 build 17763 or later, or Windows 11, on x64.
- Microsoft App Installer providing `winget` on `PATH`, and network access to the selected sources.
- Administrator rights for source add/remove/reset and for Windows Update installation. The app starts as a normal user and never re-launches itself elevated.

## First setup

1. Run the NSIS setup, or extract the entire portable ZIP and open `OnlyWinget.exe` (see [Releases](https://github.com/dennidalpos/OnlyWinget/releases); if there are no assets, build from source).
2. Open **Sources** and check which sources are enabled.
3. Open **Packages → Search** to find applications, or create a preset under **Packages → Presets**.

Settings and workspace live in `%LOCALAPPDATA%\OnlyWinget`, even for the portable ZIP.

## Usage

- **Packages**: build presets, import or export them, install or uninstall the selected entries.
- **Updates**: scan WinGet or Windows Update and install the selected or all results.
- **Activity**: read, filter and export logs. Cancelling does not roll back installs: check the installed state before retrying.

## Build and test

Use PowerShell 7 and the .NET SDK pinned in [global.json](global.json); setup packaging also needs NSIS 3.x.

```powershell
pwsh -File .\scripts\run.ps1 -Task Setup -NonInteractive
pwsh -File .\scripts\run.ps1 -Task Build -Configuration Release -NoRestore -NonInteractive
pwsh -File .\scripts\run.ps1 -Task Dev -Configuration Release -NoRestore -NonInteractive
```

Set `ONLYWINGET_SKIP_AUTO_INSTALL=1` to fail instead of installing missing prerequisites. GitHub Actions are disabled: use the local scripts ([script reference](scripts/README.md), [operations](docs/operations.md)).

## Documentation and security

[User guide](docs/user-guide.md) ([Italiano](docs/user-guide.it.md)) · [Architecture](docs/architecture.md) · [Release checks](docs/release.md) · [Security policy](SECURITY.md) · [License](LICENSE) (proprietary; credits in [CREDITS.md](CREDITS.md)).
