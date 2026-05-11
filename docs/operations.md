# Build, Test, and Delivery

## Documentation map

Use the repository documents with this split:

- [`../README.md`](../README.md): product-facing overview for GitHub
- [`architecture.md`](architecture.md): application structure, runtime behavior, and packaging model
- [`operations.md`](operations.md): setup, canonical commands, CI reproduction, and troubleshooting
- [`../PROJECT_STATUS.json`](../PROJECT_STATUS.json): current audit snapshot and residual open work when present

## Environment baseline

This repository is Windows-first and uses PowerShell for operational entrypoints.

Required toolchain from the repository:

- .NET SDK pinned through [`global.json`](../global.json) to `9.0.100`
- application target framework: `net8.0-windows`
- PowerShell 7+ for scripts in `scripts/`
- `winget` on `PATH` for normal app use and optional smoke tests
- bundled WiX 3.14 binaries in `tools/wix314-binaries/` for packaging

The setup script restores NuGet dependencies in locked mode. WiX is not installed by setup because the packaging flow resolves the bundled binaries first.
End-user setup artifacts are self-contained; the installed app does not require a separate .NET Desktop Runtime installation.

## Canonical entrypoints

### Verification command map

- setup: `pwsh -ExecutionPolicy Bypass -File .\scripts\install.ps1`
- restore: `dotnet restore .\OnlyWinget.sln --locked-mode`
- format: `dotnet format .\OnlyWinget.sln --verify-no-changes --no-restore`
- lint: `pwsh -ExecutionPolicy Bypass -File .\scripts\build.ps1 -Configuration Release -WarnAsError -NoRestore`
- typecheck: no standalone repository command is versioned; the C# compilation in `scripts/build.ps1` and `scripts/internal/build-gate.ps1` is the supported typecheck path
- test: `dotnet test .\tests\OnlyWinget.Tests\OnlyWinget.Tests.csproj -c Release --no-restore --results-directory .\artifacts\test-results --logger "trx;LogFileName=unit-tests.trx"`
- build: `pwsh -ExecutionPolicy Bypass -File .\scripts\build.ps1 -Configuration Release -NoRestore`
- run: `.\artifacts\bin\OnlyWinget\Release\net8.0-windows\OnlyWinget.exe` after a successful build
- packaging: `pwsh -ExecutionPolicy Bypass -File .\scripts\package.ps1 -Configuration Release -NoRestore`
- installer lifecycle: `pwsh -ExecutionPolicy Bypass -File .\scripts\validate-installer-lifecycle.ps1 -Configuration Release -NoRestore`
- CI reproduction locally: `pwsh -ExecutionPolicy Bypass -File .\scripts\internal\build-gate.ps1 -Configuration Release`
- deploy: no repository command or documented workflow is versioned for deploy
- release: no repository command or documented workflow is versioned for release

### Restore

```powershell
dotnet restore .\OnlyWinget.sln --locked-mode
```

Equivalent supported script:

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\install.ps1
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

Equivalent supported script:

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\run.ps1 -Configuration Release
```

### Repository verification gate

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\internal\build-gate.ps1 -Configuration Release
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
pwsh -ExecutionPolicy Bypass -File .\scripts\package.ps1 -Configuration Release
```

What it does:

- resolves WiX tools from `tools/wix314-binaries/` first, then from `PATH`
- runs self-contained `dotnet publish` for `win-x86` and `win-x64`
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

The Burn setup UI includes an optional desktop shortcut checkbox. It is off by default and passes `INSTALLDESKTOPSHORTCUT=1` to the selected internal MSI only when the user enables it.

### Installer lifecycle validation

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\validate-installer-lifecycle.ps1 -Configuration Release -NoRestore
```

What it does:

- requires elevated PowerShell and a clean local OnlyWinget install state
- generates a previous-version setup baseline when `-PreviousSetupPath` is not supplied
- generates or resolves the current setup EXE
- installs the previous setup, upgrades through the current setup, launches the app, repairs the current setup, and uninstalls it
- verifies one visible Add/Remove Programs entry, the Program Files app folder, Start Menu shortcut creation, default no-desktop-shortcut behavior, application directory removal, and `%LOCALAPPDATA%\OnlyWinget` preservation
- writes logs and `installer-lifecycle-report.txt` under `artifacts\installer-validation\<Configuration>\`

Relevant parameters:

- `-CurrentSetupPath`: use an existing current setup EXE instead of generating one
- `-PreviousSetupPath`: use an existing previous setup EXE for release-artifact upgrade validation
- `-PreviousVersion`: generate the previous setup baseline with an explicit MSI-compatible version
- `-SkipPackage`: skip setup generation and use existing artifacts

### Cleanup

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\clean.ps1 -Configuration Release
```

What it does:

- runs `dotnet clean` on supported `.csproj` files
- removes generated `bin`, `obj`, `TestResults`, and `artifacts/` directories
- optionally removes `.vs` and `packages` with `-All`

## Script classification

Scripts present in `scripts/` are intentionally split between root entrypoints, agent maintenance, and internal helpers.

| Script | Scope | Purpose | Inputs | Outputs | Dependencies | Referenced by |
| --- | --- | --- | --- | --- | --- | --- |
| `scripts/install.ps1` | root entrypoint | Restore repository dependencies in locked mode. | `-ForceEvaluate` optional restore refresh. | NuGet restore state under the configured package cache and `artifacts/obj`. | `dotnet`, `OnlyWinget.sln`, `scripts/internal/ScriptHelpers.ps1`. | README, operations docs. |
| `scripts/build.ps1` | root entrypoint | Build the WPF app; also supports warning-as-error verification. | `-Configuration`, `-NoRestore`, `-StopRunningInstance`, `-WarnAsError`. | App build output under `artifacts/bin/OnlyWinget/<Configuration>/net8.0-windows/`. | `dotnet`, app csproj, `scripts/internal/ScriptHelpers.ps1`. | `scripts/internal/build-gate.ps1`, operations docs. |
| `scripts/run.ps1` | root entrypoint | Launch the built WPF app, optionally building first. | `-Configuration`, `-Build`, `-NoRestore`, `-StopRunningInstance`. | Running `OnlyWinget.exe` process. | Built app output, optional `scripts/build.ps1`, `scripts/internal/ScriptHelpers.ps1`. | operations docs. |
| `scripts/clean.ps1` | root entrypoint | Remove generated build, test, temporary, and artifact outputs. | `-Configuration`, `-StopRunningInstance`, `-DryRun`, `-All`. | Deleted `bin`, `obj`, `TestResults`, `artifacts`, and `tmp`; optionally `.vs` and `packages`. | `dotnet`, `scripts/internal/ScriptHelpers.ps1`. | operations docs. |
| `scripts/package.ps1` | root entrypoint | Build architecture-specific internal MSIs and the unified setup EXE. | `-Configuration`, `-Version`, `-NoRestore`, `-StopRunningInstance`, `-Architecture`, `-SkipBundle`. | `artifacts/dist/OnlyWinget/<Configuration>/*-setup.exe` and `msi/*.msi`. | `dotnet`, WiX 3.14 tools, app and setup sources, `scripts/internal/ScriptHelpers.ps1`. | `scripts/internal/build-gate.ps1`, operations docs, PROJECT_STATUS. |
| `scripts/validate-installer-lifecycle.ps1` | root validation entrypoint | Execute real elevated setup install, major-upgrade, launch, repair, uninstall, and artifact checks. | `-Configuration`, `-CurrentSetupPath`, `-PreviousSetupPath`, `-PreviousVersion`, `-NoRestore`, `-SkipPackage`. | Installer logs and `installer-lifecycle-report.txt` under `artifacts/installer-validation/<Configuration>/`. | elevated PowerShell, generated setup EXE, `scripts/package.ps1`, `scripts/internal/ScriptHelpers.ps1`. | operations docs, PROJECT_STATUS. |
| `scripts/internal/build-gate.ps1` | internal verification | Reproduce CI locally. | `-Configuration`, `-RunWingetSmoke`. | Build report, test results, app build output, setup EXE, internal MSIs. | `dotnet`, `scripts/build.ps1`, `scripts/package.ps1`, tests, WiX tools, `scripts/internal/ScriptHelpers.ps1`. | GitHub Actions, README, operations docs. |
| `scripts/agents/analyze-scripts.ps1` | agent maintenance | Run PSScriptAnalyzer over active PowerShell scripts. | None. | Console analyzer report. | `PSScriptAnalyzer`, `scripts/agents/PSScriptAnalyzerSettings.psd1`. | Manual agent maintenance only; not CI. |
| `scripts/agents/PSScriptAnalyzerSettings.psd1` | agent maintenance config | Configure script analysis rules. | Analyzer invocation. | Rule exclusions consumed by PSScriptAnalyzer. | PSScriptAnalyzer. | `scripts/agents/analyze-scripts.ps1`. |
| `scripts/internal/ScriptHelpers.ps1` | internal helper | Shared command, path, and running-process guards. | Dot-sourced by scripts. | Helper functions in the caller scope. | PowerShell runtime. | Root operational scripts. |

No active script is currently classified as legacy. The former package script name `build-msi.ps1` was consolidated into `package.ps1`; update callers to the package entrypoint instead of reintroducing a wrapper.

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

- `scripts/internal/build-gate.ps1` skips the real `winget` smoke tests unless `-RunWingetSmoke` is supplied

Action:

- treat the default gate as the canonical local verification path
- enable `-RunWingetSmoke` only when you explicitly want live `winget` validation in the current environment

### Installer validation requires elevation

Observed behavior:

- the repository can generate the unified setup EXE and the internal MSIs without installing them
- `scripts\validate-installer-lifecycle.ps1` executes the real setup lifecycle and therefore requires elevated PowerShell
- the validation script refuses to run when an existing OnlyWinget installation is present

Action:

- run installer lifecycle validation only on a clean or dedicated Windows host
- pass `-PreviousSetupPath` when validating upgrade from an official historical release artifact

### Deploy and release commands are not versioned here

Observed behavior:

- the repository contains build and packaging entrypoints, but no versioned deploy or release workflow

Action:

- stop local release work at setup, build, test, run, packaging, and installer lifecycle validation unless a separate repository-owned deploy or release workflow is added

## CI

GitHub Actions uses `.github/workflows/build-gate.yml`.

The workflow:

- runs on `windows-latest`
- restores with the SDK from `global.json`
- executes `scripts/internal/build-gate.ps1`
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

The repository scripts and sources support app build, test execution, MSI/setup generation, and elevated installer lifecycle validation locally.

Repository-evidenced state:

- Unified setup generation is scripted and versioned.
- Internal x86 and x64 MSI generation is scripted and versioned.
- The installer is per-machine and therefore requires administrative privileges for real install, upgrade, repair, and uninstall validation.
- `scripts\validate-installer-lifecycle.ps1` can generate a previous-version setup baseline or consume an official previous setup with `-PreviousSetupPath`.
- `PROJECT_STATUS.json` records the latest audit snapshot and stays empty of tasks when no concrete residual backlog is open.

Installer lifecycle results are written to `artifacts\installer-validation\<Configuration>\installer-lifecycle-report.txt`.

## Tracking

`PROJECT_STATUS.json` is the active repository tracking file for residual verification drift that is still open, blocked, risky, or not verifiable from the current workspace alone. If no such work is present, its `tasks` array remains empty.
