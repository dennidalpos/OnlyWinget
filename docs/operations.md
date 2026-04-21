# Build, Test, and Delivery

## Documentation map

Use the repository documents with this split:

- [`../README.md`](../README.md): product-facing overview for GitHub
- [`architecture.md`](architecture.md): application structure, runtime behavior, and packaging model
- [`operations.md`](operations.md): setup, canonical commands, CI reproduction, and troubleshooting
- [`../PROJECT_STATUS.json`](../PROJECT_STATUS.json): residual blocked or non-verifiable work that remains open after repository-based validation

## Environment baseline

This repository is Windows-first and uses PowerShell for operational entrypoints.

Observed toolchain from the repository:

- .NET SDK pinned through [`global.json`](../global.json) to `9.0.100`
- application target framework: `net8.0-windows`
- PowerShell scripts in `scripts/`
- bundled WiX 3.14 binaries in `tools/wix314-binaries/`

## Canonical entrypoints

### Verification command map

- setup: `dotnet restore .\OnlyWinget.sln --locked-mode`
- format: `dotnet format .\OnlyWinget.sln --verify-no-changes --no-restore`
- lint: `pwsh -ExecutionPolicy Bypass -File .\scripts\build.ps1 -Configuration Release -WarnAsError -NoRestore`
- typecheck: no standalone repository command is versioned; the C# compilation in `scripts/build.ps1` and `scripts/build-gate.ps1` is the supported typecheck path
- test: `dotnet test .\tests\OnlyWinget.Tests\OnlyWinget.Tests.csproj -c Release --no-restore --results-directory .\artifacts\test-results --logger "trx;LogFileName=unit-tests.trx"`
- build: `pwsh -ExecutionPolicy Bypass -File .\scripts\build.ps1 -Configuration Release -NoRestore`
- run: `.\artifacts\bin\OnlyWinget\Release\net8.0-windows\OnlyWinget.exe` after a successful build
- packaging: `pwsh -ExecutionPolicy Bypass -File .\scripts\build-msi.ps1 -Configuration Release -NoRestore`
- CI reproduction locally: `pwsh -ExecutionPolicy Bypass -File .\scripts\build-gate.ps1 -Configuration Release`
- deploy: no repository command or documented workflow is versioned for deploy
- release: no repository command or documented workflow is versioned for release

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

### App run

After a successful build, launch the generated WPF executable directly:

```powershell
.\artifacts\bin\OnlyWinget\Release\net8.0-windows\OnlyWinget.exe
```

The repository does not version a separate `run` script. The supported local run path is the built app output.

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

## Operational scripts

Scripts present in `scripts/` that are relevant to day-to-day repository operations:

- `scripts/build-gate.ps1`: local reproduction of the CI verification gate
- `scripts/build.ps1`: app build entrypoint, also used as the repository lint gate through `-WarnAsError`
- `scripts/build-msi.ps1`: packaging entrypoint for the internal MSIs and the unified setup EXE
- `scripts/clean.ps1`: removes generated build, test, and artifact outputs
- `scripts/_analyze.ps1`: optional PowerShell script analysis using `PSScriptAnalyzer`; useful for script maintenance, but not currently wired into CI

## Troubleshooting

### `winget` is unavailable

Observed behavior:

- the app startup flow blocks normal use when `winget` is missing
- the repository scripts assume `winget` is available for real smoke tests and for normal app behavior

Action:

- install or repair Microsoft App Installer so that `winget` is available on `PATH`
- rerun the repository verification after `winget --version` succeeds

### Build or packaging is blocked by a running app instance

Observed behavior:

- `scripts/build.ps1` and `scripts/clean.ps1` fail when `OnlyWinget.exe` is still running and output files are locked

Action:

- close the app manually, or rerun supported scripts with `-StopRunningInstance` when that parameter is available

### Smoke tests do not run in the default gate

Observed behavior:

- `scripts/build-gate.ps1` skips the real `winget` smoke tests unless `-RunWingetSmoke` is supplied

Action:

- treat the default gate as the canonical local verification path
- enable `-RunWingetSmoke` only when you explicitly want live `winget` validation in the current environment

### Packaging verification is not the same as install verification

Observed behavior:

- the repository can generate the unified setup EXE and the internal MSIs
- the repository does not, by itself, prove clean-host install, upgrade, uninstall, or rollback execution

Action:

- keep packaging, deploy, and release as separate phases
- use [`../PROJECT_STATUS.json`](../PROJECT_STATUS.json) for packaging validation work that cannot be closed from the current workspace alone

### Deploy and release commands are not versioned here

Observed behavior:

- the repository contains build and packaging entrypoints, but no versioned deploy or release workflow

Action:

- stop local verification at setup, build, test, run, and packaging unless a separate repository-owned workflow is added

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

The repository scripts and sources support app build, test execution, and MSI/setup generation locally, but packaging generation and installation verification are not the same thing.

Repository-evidenced state:

- Unified setup generation is scripted and versioned.
- Internal x86 and x64 MSI generation is scripted and versioned.
- The installer is per-machine and therefore requires administrative privileges for real install, upgrade, and uninstall validation.
- The packaging scripts generate installer artifacts for the current code in this workspace, but they do not provide a supported previous release artifact to upgrade from.
- Residual verification work that remains open after source and script validation is tracked in `PROJECT_STATUS.json`.

The repository does not, by itself, prove that a clean-host install, major upgrade, or final uninstall run has been executed successfully. Those checks should be treated as packaging verification work, not as build verification.

## Tracking

`PROJECT_STATUS.json` is the active repository tracking file for residual verification drift that is still open, blocked, risky, or not verifiable from the current workspace alone.
