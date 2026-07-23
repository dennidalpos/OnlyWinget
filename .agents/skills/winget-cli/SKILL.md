---
name: winget-cli
description: Official developer skill for Windows Package Manager (winget CLI) integration, commands, manifest schemas, source preference handling, silent install switches, elevated execution, and C# process invocation.
---

# Windows Package Manager (winget) CLI Skill

This skill provides guidelines and specifications for interacting with the official **Windows Package Manager (`winget`)** CLI, PowerShell module, REST source endpoints, and C# process interop within Windows applications.

Source: [Microsoft Learn - Windows Package Manager Documentation](https://learn.microsoft.com/windows/package-manager/winget/)

## 1. Core Architecture & Sources

Winget manages software packages using structured repositories (sources):
- **Default Sources**: `winget` (Community repository indexed via GitHub/CDN) and `msstore` (Microsoft Store apps).
- **Custom Sources**: REST-based package catalogs added via `winget source add -n <name> -a <url> -t Microsoft.Rest`.
- **Source Management**:
  - `winget source list`: Lists configured sources.
  - `winget source update`: Refreshes package indexes.
  - `winget source reset`: Resets sources to defaults.

## 2. CLI Command Reference

### Package Discovery & Inspection
- **Search**: `winget search <query> [--source <source>] [--exact] [--id <id>] [--name <name>]`
- **Show Details**: `winget show <package-id> [--source <source>]`
- **List Installed**: `winget list [<query>] [--source <source>]`

### Package Operations
- **Install**: `winget install <package-id> [--scope user|machine] [--silent|--interactive] [--accept-package-agreements] [--accept-source-agreements] [--location <path>]`
- **Upgrade**: `winget upgrade <package-id> [--all] [--silent]`
- **Uninstall**: `winget uninstall <package-id> [--silent]`
- **Pin / Unpin**: `winget pin add <package-id>` / `winget pin remove <package-id>`

### Batch Export / Import
- **Export**: `winget export -o <path.json> [--include-versions]`
- **Import**: `winget import -i <path.json> [--ignore-unavailable] [--ignore-versions]`

## 3. Package Manifest Schema (v1.0 - v1.9)

Winget package manifests consist of YAML files defining package metadata:
- **`PackageIdentifier`**: Unique ID formatted as `Publisher.App` (e.g. `Git.Git`, `7zip.7zip`).
- **`InstallerType`**: `msi`, `msix`, `exe`, `nullsoft` (NSIS), `inno`, `burn`, `wix`, `zip`.
- **`Scope`**: `user` or `machine`.
- **`Switches`**:
  - `Silent`: `/S`, `/silent`, `/qn`, etc.
  - `SilentWithProgress`: `/passive`, etc.
  - `Custom`: Custom arguments passed directly to the underlying installer.

## 4. Exit Codes & Diagnostics

Common winget process exit codes to check in C# / PowerShell wrappers:
- `0x00000000` (`0`): Success.
- `0x8a150014` (`-1978335212`): No package or update found.
- `0x8a15000f` (`-1978335217`): Package already installed or up to date.
- `0x8a150012` (`-1978335214`): Command cancelled by user.
- `0x80070005` (`-2147024891`): Access denied / requires administrative elevation.

## 5. Integration Guidelines in C# / .NET 10

When wrapping `winget.exe` or PowerShell `Microsoft.WinGet.Client` in C#:

1. **Capability Checks**: Always check `winget` binary existence via `ISystemCapabilityService` before invoking commands.
2. **Process Execution**: Use asynchronous process wrappers with cancellation token support. Pass real `CancellationToken`s.
3. **Throttling**: Limit concurrent `winget` process execution using `SemaphoreSlim` (e.g., maximum 4 concurrent processes) to prevent high CPU/disk contention.
4. **Elevation**: Handle machine-scope installer elevation gracefully. Do not assume read-only searches require elevation.
5. **Output Parsing**: Prefer parsing JSON outputs when available, or robust Regex line matching for search/list operations.
