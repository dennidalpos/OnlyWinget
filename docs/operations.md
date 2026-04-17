# Build, Test, and Delivery

## Environment baseline

This repository is Windows-first and uses PowerShell for operational entrypoints.

Observed toolchain from the repository:

- .NET SDK pinned through [`global.json`](../global.json) to `9.0.100`
- application target framework: `net8.0-windows`
- PowerShell scripts in `scripts/`
- bundled WiX 3.14 binaries in `tools/wix314-binaries/`

## Canonical entrypoints

### Restore

```powershell
dotnet restore .\OnlyWinget.sln --locked-mode
```

### App build

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\build.ps1 -Configuration Release
```

What it does:

- validates the solution and app project paths
- optionally restores dependencies
- builds `src/OnlyWinget/OnlyWinget.csproj`
- writes outputs under `artifacts/bin/OnlyWinget/<Configuration>/net8.0-windows/`

### Repository verification gate

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\build-gate.ps1 -Configuration Release
```

What it does:

1. restores the solution in locked mode
2. runs `dotnet format --verify-no-changes`
3. runs the app build with warnings treated as errors
4. runs the test project
5. optionally runs real `winget` smoke tests when `-RunWingetSmoke` is supplied
6. rebuilds the app
7. builds the unified setup and internal MSI packages
8. writes an artifact summary to `artifacts/build-report.txt`

Primary outputs:

- `artifacts/build-report.txt`
- `artifacts/test-results/**/*.trx`
- `artifacts/dist/OnlyWinget/<Configuration>/OnlyWinget-<version>-setup.exe`
- `artifacts/dist/OnlyWinget/<Configuration>/msi/*.msi`

### Windows packaging

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\build-msi.ps1 -Configuration Release
```

What it does:

- resolves WiX tools from `tools/wix314-binaries/` first, then from `PATH`
- runs framework-dependent `dotnet publish` for `win-x86` and `win-x64`
- harvests each publish output with `heat.exe`
- compiles and links internal architecture-specific MSIs with `candle.exe` and `light.exe`
- compiles and links one WiX Burn setup EXE containing both MSIs
- emits the primary setup under `artifacts/dist/OnlyWinget/<Configuration>/`
- emits internal MSIs under `artifacts/dist/OnlyWinget/<Configuration>/msi/`

Relevant parameters:

- `-Version`: overrides the installer product version
- `-NoRestore`: skips the packaging restore step
- `-StopRunningInstance`: prepares the build by closing a running `OnlyWinget` process
- `-Architecture x86|x64`: builds one internal MSI only
- `-SkipBundle`: skips the unified setup and leaves only internal MSI output

Packaging prerequisites already versioned in the repository:

- `src/OnlyWinget.Setup/OnlyWinget.Setup.wxs`
- `src/OnlyWinget.Setup/OnlyWinget.Bundle.wxs`
- `src/OnlyWinget.Setup/License.rtf`
- `scripts/remove-project-services.ps1`
- `tools/wix314-binaries/`

The user-facing installer is the Burn setup EXE. It contains the x86 and x64 MSIs and uses package conditions to run the x64 MSI only when `VersionNT64` is true; otherwise it runs the x86 MSI. The architecture-specific MSIs also include direct-execution launch conditions: the x64 MSI blocks 32-bit Windows, and the x86 MSI is reserved for 32-bit Windows rather than direct use on 64-bit Windows.

### Cleanup

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\clean.ps1 -Configuration Release
```

What it does:

- runs `dotnet clean` on supported `.csproj` files
- removes generated `bin`, `obj`, `TestResults`, and `artifacts/` directories
- optionally removes `.vs` and `packages` with `-All`

## CI

GitHub Actions uses `.github/workflows/build-gate.yml`.

The workflow:

- runs on `windows-latest`
- restores with the SDK from `global.json`
- executes `scripts/build-gate.ps1`
- uploads the build report, test results, unified setup, and internal MSI artifacts

An optional `workflow_dispatch` input can enable the `winget` smoke tests.

## Tests

The repository uses xUnit in `tests/OnlyWinget.Tests`.

The test suite contains:

- unit and service-level tests for the application logic
- coverage for paused preset rows, `winget` Unicode output parsing, and runtime log retention
- optional smoke tests in `WingetSmokeTests.cs`

The smoke tests are skipped unless the environment variable below is set by the build gate:

```powershell
$env:ONLYWINGET_RUN_WINGET_SMOKE='1'
```

## Verification boundaries

The repository scripts and sources support MSI generation locally, but packaging verification and installation verification are not the same thing.

State currently reflected by the repository:

- Unified setup generation is scripted and versioned.
- Internal x86 and x64 MSI generation is scripted and versioned.
- The installer is per-machine and therefore requires administrative privileges for real install, upgrade, and uninstall validation.
- Local install, same-version rerun, launch, and uninstall validation have been executed on an elevated x64 Windows host.
- `PROJECT_STATUS.json` still tracks one blocked packaging check:
  - major upgrade validation from a supported previous version

Those checks should be treated as release-blocking packaging verification, not as build verification.

## Tracking

`PROJECT_STATUS.json` is the active repository tracking file for open verification work that is still pending after local script-based validation.
