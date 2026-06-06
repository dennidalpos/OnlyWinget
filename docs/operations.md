# Build, Test, and Delivery

## Documentation map

Use the repository documents with this split:

- [`../README.md`](../README.md): product-facing overview for GitHub
- [`architecture.md`](architecture.md): application structure, runtime behavior, and packaging model
- [`operations.md`](operations.md): setup, canonical commands, CI reproduction, and troubleshooting
- [`release.md`](release.md): versioning, release candidate verification, tagging, and GitHub release publishing
- [`../scripts/README.md`](../scripts/README.md): script inventory, invocation map, and migration notes
- [`../PROJECT_STATUS.json`](../PROJECT_STATUS.json): current incomplete and actionable repository todos when present

## Environment baseline

This repository is Windows-first and uses PowerShell for operational entrypoints.

Required toolchain from the repository:

- .NET SDK pinned through [`global.json`](../global.json) to `9.0.100` with `rollForward` set to `latestFeature`
- application target framework: `net8.0-windows`
- PowerShell 7+ for scripts in `scripts/`
- optional `PSScriptAnalyzer` module for PowerShell script linting
- `winget` on `PATH` for normal app use and optional smoke tests
- WiX Toolset 3.x for packaging

NuGet is the package manager. Restore is locked through `Directory.Build.props` and the project `packages.lock.json` files. The app project has no direct PackageReference entries; the test project declares `Microsoft.NET.Test.Sdk`, `xunit`, and `xunit.runner.visualstudio`.

The setup script restores NuGet dependencies in locked mode. WiX is not installed by setup. The packaging flow resolves WiX from `tools/wix314-binaries/` when present, then `ONLYWINGET_WIX_BIN`, the WiX `WIX` environment variable, standard Program Files install locations, and finally `PATH`.
End-user setup artifacts are self-contained; the installed app does not require a separate .NET Desktop Runtime installation.

## Clean Windows Client Prerequisites

| Prerequisite | Required by | Strategy | Verification |
| --- | --- | --- | --- |
| Windows 10 or Windows 11 | WPF desktop app, Win32 shell integration, winget client workflows | Required manually. The WiX Burn setup and internal MSIs block unsupported Windows versions before applying payloads. | `winver` |
| .NET Desktop Runtime for `net8.0-windows` | WPF application runtime | Included in the setup payload through self-contained `dotnet publish` for `win-x86` and `win-x64`. No separate runtime install is required on end-user clients. | Installed app starts without `dotnet --list-runtimes`; package build uses `--self-contained true`. |
| Microsoft App Installer with `winget` 1.x | Search, install, uninstall, update, and package interrogation flows | Required manually because it is the external package manager the app controls. First startup blocks before showing the main window when `winget --version` cannot run, and offers the official Microsoft App Installer page. | `winget --version` |
| Windows PowerShell `powershell.exe` | Wide-console wrapper for some `winget search`, `list`, and `upgrade` calls | Managed by the supported Windows 10/11 baseline; no separate install is required on clean supported clients. | `powershell.exe -NoLogo -NoProfile -Command "$PSVersionTable.PSVersion"` |
| Visual C++ Redistributable, local database engines, local services, drivers | Not required directly by current source or installer | Not packaged and not required. | No package, service, driver, or database dependency is declared in source or setup. |

## Canonical entrypoints

### Fresh workstation setup

From a clean checkout on Windows:

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\setup.ps1
pwsh -ExecutionPolicy Bypass -File .\scripts\format.ps1 -NoRestore
pwsh -ExecutionPolicy Bypass -File .\scripts\typecheck.ps1 -Configuration Release -NoRestore
pwsh -ExecutionPolicy Bypass -File .\scripts\test.ps1 -Configuration Release -NoRestore
```

This sequence restores locked NuGet dependencies, verifies formatting, compiles C# with warnings as errors, and runs the unit test project. It does not require WiX unless packaging or the full build gate is run.

### Verification command map

- setup: `pwsh -ExecutionPolicy Bypass -File .\scripts\setup.ps1`
- restore: `dotnet restore .\OnlyWinget.sln --locked-mode`
- dependency audit: `dotnet list .\OnlyWinget.sln package --vulnerable --include-transitive`
- format: `pwsh -ExecutionPolicy Bypass -File .\scripts\format.ps1 -NoRestore`
- script lint: `pwsh -ExecutionPolicy Bypass -File .\scripts\lint.ps1`
- typecheck: `pwsh -ExecutionPolicy Bypass -File .\scripts\typecheck.ps1 -Configuration Release -NoRestore`
- test: `pwsh -ExecutionPolicy Bypass -File .\scripts\test.ps1 -Configuration Release -NoRestore`
- build: `pwsh -ExecutionPolicy Bypass -File .\scripts\build.ps1 -Configuration Release -NoRestore`
- run: `.\artifacts\bin\OnlyWinget\Release\net8.0-windows\OnlyWinget.exe` after a successful build
- packaging: `pwsh -ExecutionPolicy Bypass -File .\scripts\package.ps1 -Configuration Release -NoRestore`
- installer lifecycle: `pwsh -ExecutionPolicy Bypass -File .\scripts\validate-installer-lifecycle.ps1 -Configuration Release -NoRestore`
- CI reproduction locally: `pwsh -ExecutionPolicy Bypass -File .\scripts\check.ps1 -Configuration Release`
- deploy: no repository command or documented workflow is versioned for deploy
- release: follow [`release.md`](release.md) for version changes, local and hosted verification, tagging, and GitHub release publishing

### Restore

```powershell
dotnet restore .\OnlyWinget.sln --locked-mode
```

Equivalent supported script:

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\setup.ps1
```

### PowerShell script lint

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\lint.ps1
```

The script lint command requires the optional `PSScriptAnalyzer` module. When the module is missing, the command reports script lint as `not_available` and exits successfully so a clean workstation can distinguish a missing optional analyzer from analyzer findings.

Install the module for local linting:

```powershell
Install-Module PSScriptAnalyzer -Scope CurrentUser -Repository PSGallery
```

Use `-Required` when a stricter local or CI gate should fail if the analyzer is missing:

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\lint.ps1 -Required
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
pwsh -ExecutionPolicy Bypass -File .\scripts\dev.ps1 -Configuration Release
```

### Repository verification gate

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\check.ps1 -Configuration Release
```

What it does:

1. restores the solution in locked mode
2. runs `scripts/format.ps1 -NoRestore`
3. runs `scripts/lint.ps1`, which checks PowerShell scripts when PSScriptAnalyzer is installed
4. runs `scripts/typecheck.ps1 -NoRestore`, which builds with warnings treated as errors
5. runs the test project
6. records real `winget` smoke tests as `not_run` by default, or runs them when `-RunWingetSmoke` is supplied
7. rebuilds the app
8. builds the unified setup and internal MSI packages
9. writes an artifact summary to `artifacts/build-report.txt`

Primary outputs:

- `artifacts/build-report.txt`
- `artifacts/test-results/**/*.trx`
- `artifacts/dist/OnlyWinget/<Configuration>/OnlyWinget-<version>-setup.exe`
- `artifacts/dist/OnlyWinget/<Configuration>/msi/*.msi`

The gate requires WiX Toolset 3.x because packaging is part of the gate. Use the smaller fresh-workstation sequence above when validating code changes on a machine that does not have WiX.

### Windows packaging

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\package.ps1 -Configuration Release
```

What it does:

- resolves WiX tools from `tools/wix314-binaries/`, `ONLYWINGET_WIX_BIN`, the WiX `WIX` environment variable, standard Program Files install locations, or `PATH`
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
- `src/OnlyWinget.Setup/BurnResponsiveTheme.xml`
- `src/OnlyWinget.Setup/BurnResponsiveTheme.wxl`
- `src/OnlyWinget.Setup/Assets/WixUIBanner.bmp`
- `src/OnlyWinget.Setup/Assets/WixUIDialog.bmp`
- `src/OnlyWinget/Assets/OnlyWinget.ico`
- `src/OnlyWinget/Assets/OnlyWinget-icon.png`

Optional local packaging prerequisite:

- `tools/wix314-binaries/` can be added to pin a repository-local WiX toolset, but it is not required when WiX Toolset 3.x is installed on the machine.

The user-facing installer is the Burn setup EXE. It blocks unsupported Windows versions before applying payloads, contains the x86 and x64 MSIs, and uses package conditions to run the x64 MSI only when `VersionNT64` is true; otherwise it runs the x86 MSI. The architecture-specific MSIs also include direct-execution launch conditions: both MSIs block Windows builds earlier than Windows 10 build 10240, the x64 MSI blocks 32-bit Windows, and the x86 MSI is reserved for 32-bit Windows rather than direct use on 64-bit Windows.

Uninstall removes MSI-tracked application files, Start Menu shortcuts, the optional desktop shortcut, and installer-owned directories when they are empty. It intentionally does not recursively delete a path read from the registry.

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

### Release publishing

Release publishing is documented in [`release.md`](release.md).

The release flow is intentionally gated:

1. Update the version in `src/OnlyWinget/OnlyWinget.csproj`.
2. Run the local `scripts/check.ps1` gate.
3. Run elevated installer lifecycle validation on a clean or disposable Windows host.
4. Run the hosted GitHub Actions `build-gate` workflow for the exact release commit.
5. Tag the verified commit as `vMAJOR.MINOR.PATCH`.
6. Publish a GitHub release from that tag and attach the unified setup EXE.

The unified setup EXE is the supported end-user release artifact. Internal MSI files are generated
for the setup bundle and maintainer diagnostics.

### Cleanup

```powershell
pwsh -ExecutionPolicy Bypass -File .\scripts\clean.ps1 -Configuration Release
```

What it does:

- runs `dotnet clean` on supported `.csproj` files
- removes generated root directories such as `artifacts/`, `tmp/`, `build/`, `dist/`, `out/`, `publish/`, `coverage/`, `logs/`, and `reports/`
- removes nested generated `bin`, `obj`, and `TestResults` directories outside protected repository areas
- removes generated file patterns such as `*.binlog`, `*.cache`, `*.coverage`, `*.log`, `*.tmp`, and `*.trx`
- optionally removes `.vs` and `packages` with `-All`
- optionally clears NuGet/.NET caches with `-NuGetCache`; `-All` also enables this cache cleanup

## Script classification

Scripts present in `scripts/` are intentionally split between root entrypoints and support files under `scripts/support/`. Root operational scripts use the canonical names from `AGENTS.md`; the installer lifecycle validator remains as a separate release-only entrypoint because it requires elevated execution and mutates machine install state.

| Script | Scope | Purpose | Inputs | Outputs | Dependencies | Referenced by |
| --- | --- | --- | --- | --- | --- | --- |
| `scripts/setup.ps1` | canonical root entrypoint | Restore repository dependencies in locked mode. | `-ForceEvaluate` optional restore refresh. | NuGet restore state under the configured package cache and `artifacts/obj`. | `dotnet`, `OnlyWinget.sln`, `scripts/support/ScriptHelpers.ps1`. | README, operations docs. |
| `scripts/dev.ps1` | canonical root entrypoint | Launch the built WPF app, optionally building first. | `-Configuration`, `-Build`, `-NoRestore`, `-StopRunningInstance`. | Running `OnlyWinget.exe` process. | Built app output, optional `scripts/build.ps1`, `scripts/support/ScriptHelpers.ps1`. | README, operations docs. |
| `scripts/check.ps1` | canonical root verification gate | Reproduce CI locally through the supported sequential checks. | `-Configuration`, `-RunWingetSmoke`. | Build report, test results, app build output, setup EXE, internal MSIs. | `dotnet`, `scripts/format.ps1`, `scripts/lint.ps1`, `scripts/typecheck.ps1`, `scripts/build.ps1`, `scripts/package.ps1`, tests, WiX tools, `scripts/support/ScriptHelpers.ps1`. | GitHub Actions, README, operations docs. |
| `scripts/format.ps1` | canonical root entrypoint | Verify repository formatting, or apply formatting with `-Fix`. | `-Fix`, `-NoRestore`. | Console format report; source edits only with `-Fix`. | `dotnet`, `OnlyWinget.sln`, `scripts/support/ScriptHelpers.ps1`. | README, operations docs. |
| `scripts/lint.ps1` | canonical root entrypoint | Run PowerShell script lint. | `-Required` fails when PSScriptAnalyzer is missing. | Console analyzer report or not_available warning. | Optional `PSScriptAnalyzer`, `scripts/support/PSScriptAnalyzerSettings.psd1`. | README, operations docs. |
| `scripts/typecheck.ps1` | canonical root entrypoint | Compile C# with warnings as errors. | `-Configuration`, `-NoRestore`, `-StopRunningInstance`. | App build output under `artifacts/bin/OnlyWinget/<Configuration>/net8.0-windows/`. | `scripts/build.ps1`, `dotnet`, restored packages. | README, operations docs. |
| `scripts/test.ps1` | canonical root entrypoint | Run the xUnit test project and optional live winget smoke tests. | `-Configuration`, `-NoRestore`, `-NoBuild`, `-RunWingetSmoke`. | TRX files under `artifacts/test-results`. | `dotnet`, tests, optional winget sources. | README, operations docs. |
| `scripts/build.ps1` | root entrypoint | Build the WPF app; also supports warning-as-error verification. | `-Configuration`, `-NoRestore`, `-StopRunningInstance`, `-WarnAsError`. | App build output under `artifacts/bin/OnlyWinget/<Configuration>/net8.0-windows/`. | `dotnet`, app csproj, `scripts/support/ScriptHelpers.ps1`. | `scripts/typecheck.ps1`, `scripts/check.ps1`, operations docs. |
| `scripts/clean.ps1` | root entrypoint | Remove generated build, test, temporary, artifact, and optional cache outputs. | `-Configuration`, `-StopRunningInstance`, `-DryRun`, `-All`, `-NuGetCache`. | Deleted generated root directories, nested `bin`, `obj`, `TestResults`, generated temp/log/result files; optionally `.vs`, `packages`, and NuGet/.NET caches. | `dotnet`, `scripts/support/ScriptHelpers.ps1`. | README, operations docs. |
| `scripts/package.ps1` | root entrypoint | Build architecture-specific internal MSIs and the unified setup EXE. | `-Configuration`, `-Version`, `-NoRestore`, `-StopRunningInstance`, `-Architecture`, `-SkipBundle`. | `artifacts/dist/OnlyWinget/<Configuration>/*-setup.exe` and `msi/*.msi`. | `dotnet`, WiX 3.14 tools, app and setup sources, `scripts/support/ScriptHelpers.ps1`. | `scripts/check.ps1`, operations docs, PROJECT_STATUS. |
| `scripts/validate-installer-lifecycle.ps1` | root validation entrypoint | Execute real elevated setup install, major-upgrade, launch, repair, uninstall, and artifact checks. | `-Configuration`, `-CurrentSetupPath`, `-PreviousSetupPath`, `-PreviousVersion`, `-NoRestore`, `-SkipPackage`. | Installer logs and `installer-lifecycle-report.txt` under `artifacts/installer-validation/<Configuration>/`. | elevated PowerShell, generated setup EXE, `scripts/package.ps1`, `scripts/support/ScriptHelpers.ps1`. | operations docs, PROJECT_STATUS. |
| `scripts/support/PSScriptAnalyzerSettings.psd1` | support config | Configure script analysis rules. | Analyzer invocation. | Rule exclusions consumed by PSScriptAnalyzer. | PSScriptAnalyzer. | `scripts/lint.ps1`. |
| `scripts/support/ScriptHelpers.ps1` | support helper | Shared command checks, path normalization/safety guards, and running-process guards. | Dot-sourced by scripts. | Helper functions in the caller scope. | PowerShell runtime. | Root operational scripts. |

No active script is currently classified as legacy. The former compatibility aliases `install.ps1`, `run.ps1`, `gate.ps1`, and `lint-scripts.ps1` were consolidated into their canonical entrypoints; update callers to `setup.ps1`, `dev.ps1`, `check.ps1`, and `lint.ps1`.

## Troubleshooting

### `winget` is unavailable

Observed behavior:

- the app startup flow blocks before showing the main window when `winget` is missing
- the repository scripts assume `winget` is available for real smoke tests and for normal app behavior

Action:

- install or repair Microsoft App Installer for Windows 10/11 from Microsoft Store so that `winget` 1.x is available on `PATH`
- rerun the repository verification after `winget --version` succeeds

Message shown at first startup:

- required software: Microsoft App Installer with `winget` 1.x
- reason: OnlyWinget runs `winget` to search, install, update, and uninstall packages
- repair path: install or repair Microsoft App Installer from Microsoft Store
- verification command: `winget --version`

### Build or packaging is blocked by a running app instance

Observed behavior:

- `scripts/build.ps1` and `scripts/clean.ps1` fail when `OnlyWinget.exe` is still running and output files are locked

Action:

- close the app manually, or rerun supported scripts with `-StopRunningInstance` when that parameter is available

### Smoke tests do not run in the default gate

Observed behavior:

- `scripts/check.ps1` skips the real `winget` smoke tests unless `-RunWingetSmoke` is supplied
- default unit test results mark `WingetSmokeTests` as skipped instead of passed
- `artifacts/build-report.txt` records `SmokeTests: not_run` when the live smoke tests are disabled

Action:

- treat the default gate as the canonical local verification path
- enable `-RunWingetSmoke` only when you explicitly want live `winget` validation in the current environment

### Advanced installer arguments need secrets

Observed behavior:

- `--custom` and `--override` values are stored in `%LOCALAPPDATA%\OnlyWinget\AppsList.json`
- exported `.onlywinget.json` preset files include those values and the installer-specific package options
- imported `.onlywinget.json` rows with advanced arguments are blocked until the user reviews and saves them in the package options dialog
- command previews and runtime diagnostics redact advanced argument values

Action:

- do not type secrets directly into `--custom` or `--override`
- define a Windows environment variable before launching OnlyWinget, for example `$env:ONLYWINGET_LICENSE_KEY='...'`
- enter a placeholder such as `%ONLYWINGET_LICENSE_KEY%` in the advanced argument field; OnlyWinget expands it only when it builds the final install command
- review exported preset files before sharing them

### Imported preset or settings JSON is rejected as too large

Observed behavior:

- app data and imported `.onlywinget.json` files larger than the supported application-data limit are rejected before full deserialization
- settings JSON larger than the supported settings limit is ignored and default preferences are used

Action:

- inspect the JSON file outside the app
- remove accidental bulk content before importing or restarting the app
- do not use preset files as log, cache, or attachment containers

### Installer validation requires elevation

Observed behavior:

- the repository can generate the unified setup EXE and the internal MSIs without installing them
- `scripts\validate-installer-lifecycle.ps1` executes the real setup lifecycle and therefore requires elevated PowerShell
- the validation script refuses to run when an existing OnlyWinget installation is present

Action:

- run installer lifecycle validation only on a clean or dedicated Windows host
- pass `-PreviousSetupPath` when validating upgrade from an official historical release artifact

### Deploy command is not versioned here

Observed behavior:

- the repository contains build, packaging, and release-publishing documentation, but no versioned deploy command

Action:

- stop deploy work at release artifact publication unless a separate repository-owned deploy workflow is added

## CI

GitHub Actions uses `.github/workflows/build-gate.yml`.

The workflow:

- runs on `windows-latest`
- restores with the SDK from `global.json`
- executes `scripts/check.ps1`
- uploads the build report, test results, unified setup, and internal MSI artifacts

An optional `workflow_dispatch` input can enable the `winget` smoke tests.

## Tests

The repository uses xUnit in `tests/OnlyWinget.Tests`.

The test suite contains:

- unit and service-level tests for the application logic
- coverage for paused preset rows, `winget` Unicode output parsing, and runtime log retention
- optional smoke tests in `WingetSmokeTests.cs`

The smoke tests are discovered as skipped unless the environment variable below is set by the build gate:

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
- `PROJECT_STATUS.json` records only the current incomplete and actionable repository todos, using a top-level `todos` array.

Installer lifecycle results are written to `artifacts\installer-validation\<Configuration>\installer-lifecycle-report.txt`.

## Canonical documentation

The canonical maintained documents are:

- [`../README.md`](../README.md)
- [`architecture.md`](architecture.md)
- [`operations.md`](operations.md)
- [`release.md`](release.md)
- [`../scripts/README.md`](../scripts/README.md)
- [`../PROJECT_STATUS.json`](../PROJECT_STATUS.json)

Historical root audit notes and standalone flow notes were removed after verifying they were not referenced by README, docs, scripts, CI, or source. Keep new durable setup, architecture, or troubleshooting information in the canonical documents above instead of creating parallel root-level status reports.

## Tracking

`PROJECT_STATUS.json` is the active repository tracking file for residual verification drift that is still open, blocked, risky, or not verifiable from the current workspace alone. It uses only a top-level `todos` array; if no such work is present, the array remains empty.
