# Script Inventory

Only PowerShell entrypoints are versioned in this repository. Run commands from the repository root with PowerShell 7+ unless a script explicitly states otherwise.

Canonical operational commands use the repository-wide names from `AGENTS.md`: `setup`, `dev`, `check`, `format`, `lint`, `typecheck`, `test`, `build`, `package`, and `clean`. Older descriptive script names remain as compatibility entrypoints when they are already referenced by local workflows.

| Script | Path | Purpose | When to use | Called by | Prerequisites | Outputs | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| setup | `scripts/setup.ps1` | Restore NuGet packages in locked mode. | Fresh checkout setup or lock-file validation. | README, docs, manual use. | `dotnet`, `OnlyWinget.sln`. | NuGet restore state. | Thin wrapper around `scripts/install.ps1`; supports `-ForceEvaluate`. |
| dev | `scripts/dev.ps1` | Start the built WPF app, optionally building first. | Manual desktop smoke checks. | README, docs, manual use. | Built app output; `dotnet` when `-Build` is used. | Running `OnlyWinget.exe` process. | Thin wrapper around `scripts/run.ps1`. |
| check | `scripts/check.ps1` | Run the full repository verification gate. | Default local or CI verification. | GitHub Actions, README, docs. | `dotnet`, WiX Toolset 3.x, optional PSScriptAnalyzer. | Build report, test results, setup EXE, internal MSIs. | Thin wrapper around `scripts/gate.ps1`; supports `-RunWingetSmoke`. |
| format | `scripts/format.ps1` | Verify repository formatting, or apply formatting with `-Fix`. | Before review, CI parity checks, or local formatting. | README, docs, manual use. | `dotnet`, `OnlyWinget.sln`. | Console format report; source edits only with `-Fix`. | Uses `dotnet format`; `-NoRestore` is supported. |
| lint | `scripts/lint.ps1` | Run PowerShell script lint. | Script maintenance or local checks. | README, docs, manual use. | Optional `PSScriptAnalyzer`. | Analyzer console report. | Thin wrapper around `scripts/lint-scripts.ps1`; `-Required` fails when analyzer is missing. |
| typecheck | `scripts/typecheck.ps1` | Compile C# with warnings as errors. | Static verification before tests or review. | README, docs, manual use. | `dotnet`, restored packages. | App build output. | Thin wrapper around `scripts/build.ps1 -WarnAsError`. |
| test | `scripts/test.ps1` | Run the xUnit test project and optional live winget smoke tests. | Unit verification, optional smoke validation. | README, docs, manual use. | `dotnet`, restored packages. | TRX files under `artifacts/test-results`. | `-RunWingetSmoke` enables live winget tests. |
| build | `scripts/build.ps1` | Build the WPF app. | Local compile, package preparation, gate steps. | `scripts/typecheck.ps1`, `scripts/gate.ps1`, `scripts/package.ps1`. | `dotnet`, restored packages. | App build output under `artifacts/bin`. | Supports `-WarnAsError` for typecheck usage. |
| package | `scripts/package.ps1` | Build x86/x64 self-contained MSI payloads and the unified setup EXE. | Release artifact generation or full gate verification. | `scripts/gate.ps1`, installer lifecycle validation, docs. | `dotnet`, WiX Toolset 3.x. | Setup EXE and MSI files under `artifacts/dist`. | WiX is resolved from repository-local, env, Program Files, or PATH locations. |
| clean | `scripts/clean.ps1` | Remove generated outputs and optional local caches with path guards. | Before a fresh local build or when outputs are stale or locked. | README, docs, manual use. | `dotnet`. | Deleted generated paths, or dry-run report. | `-All` also removes `.vs`, `packages`, and NuGet/.NET caches. |
| installer lifecycle validation | `scripts/validate-installer-lifecycle.ps1` | Run real install, upgrade, launch, repair, uninstall, and local data preservation checks. | Release readiness on a clean elevated Windows host. | Docs, manual release validation. | Elevated PowerShell, generated setup EXE, clean install state. | Logs and lifecycle report under `artifacts/installer-validation`. | Not part of the default check because it mutates machine install state. |
| install compatibility | `scripts/install.ps1` | Restore NuGet packages in locked mode. | Existing local calls that have not moved to `setup.ps1`. | `scripts/setup.ps1`, older docs/workflows. | `dotnet`, `OnlyWinget.sln`. | NuGet restore state. | Kept for backward compatibility. Prefer `scripts/setup.ps1`. |
| run compatibility | `scripts/run.ps1` | Start the built app. | Existing local calls that have not moved to `dev.ps1`. | `scripts/dev.ps1`, older docs/workflows. | Built app output. | Running `OnlyWinget.exe` process. | Kept for backward compatibility. Prefer `scripts/dev.ps1`. |
| gate compatibility | `scripts/gate.ps1` | Run the full verification gate. | Existing local calls that have not moved to `check.ps1`. | `scripts/check.ps1`. | `dotnet`, WiX Toolset 3.x, optional PSScriptAnalyzer. | Build report, test results, setup EXE, internal MSIs. | Kept for backward compatibility. Prefer `scripts/check.ps1`. |
| lint compatibility | `scripts/lint-scripts.ps1` | Run PSScriptAnalyzer over active `.ps1` scripts. | Existing local calls that have not moved to `lint.ps1`. | `scripts/lint.ps1`, `scripts/gate.ps1`. | Optional `PSScriptAnalyzer`. | Analyzer console report. | Kept for backward compatibility. Prefer `scripts/lint.ps1`. |
| script helpers | `scripts/support/ScriptHelpers.ps1` | Shared command checks, path normalization, deletion guards, and running-process guards. | Dot-sourced by scripts only. | Root operational scripts. | PowerShell runtime. | Helper functions in caller scope. | Not a standalone entrypoint. |
| PSScriptAnalyzer settings | `scripts/support/PSScriptAnalyzerSettings.psd1` | Configure PowerShell analyzer rules. | Script linting. | `scripts/lint-scripts.ps1`. | PSScriptAnalyzer. | Analyzer rule configuration. | Not a standalone entrypoint. |

## Status Classification

| Path | State | Inputs | Outputs | Side effects | Dependencies |
| --- | --- | --- | --- | --- | --- |
| `scripts/setup.ps1` | active canonical | `-ForceEvaluate` | NuGet restore state | Writes restore artifacts/package cache | `dotnet` |
| `scripts/dev.ps1` | active canonical | `-Configuration`, `-Build`, `-NoRestore`, `-StopRunningInstance` | Running app process | Starts WPF app | Built executable |
| `scripts/check.ps1` | active canonical | `-Configuration`, `-RunWingetSmoke` | build report, test results, setup/MSI artifacts | Cleans generated outputs; optional live `winget` smoke tests | `dotnet`, WiX |
| `scripts/format.ps1` | active canonical | `-Fix`, `-NoRestore` | format report; optional source formatting | Edits source only with `-Fix` | `dotnet` |
| `scripts/lint.ps1` | active canonical | `-Required` | analyzer console report | None beyond module import | optional PSScriptAnalyzer |
| `scripts/typecheck.ps1` | active canonical | `-Configuration`, `-NoRestore`, `-StopRunningInstance` | app build output | Compiles app with warnings as errors | `dotnet` |
| `scripts/test.ps1` | active canonical | `-Configuration`, `-NoRestore`, `-NoBuild`, `-RunWingetSmoke` | TRX test results | Optional live `winget` smoke tests | `dotnet`, optional winget sources |
| `scripts/build.ps1` | active canonical | `-Configuration`, `-NoRestore`, `-StopRunningInstance`, `-WarnAsError` | app build output | May stop running app with `-StopRunningInstance` | `dotnet` |
| `scripts/package.ps1` | active canonical | `-Configuration`, `-Version`, `-NoRestore`, `-StopRunningInstance`, `-Architecture`, `-SkipBundle` | setup EXE, MSI files, staging files | Recreates installer staging/output artifacts | `dotnet`, WiX |
| `scripts/clean.ps1` | active canonical | `-Configuration`, `-StopRunningInstance`, `-DryRun`, `-All`, `-NuGetCache` | cleanup report | Deletes guarded generated outputs; may clear caches | `dotnet` |
| `scripts/validate-installer-lifecycle.ps1` | active release validation | setup/version parameters, `-NoRestore`, `-SkipPackage` | installer validation logs/report | Installs, repairs, uninstalls app | elevated PowerShell, setup EXE |
| `scripts/install.ps1` | compatibility | `-ForceEvaluate` | NuGet restore state | Writes restore artifacts/package cache | `dotnet` |
| `scripts/run.ps1` | compatibility | `-Configuration`, `-Build`, `-NoRestore`, `-StopRunningInstance` | Running app process | Starts WPF app | Built executable |
| `scripts/gate.ps1` | compatibility | `-Configuration`, `-RunWingetSmoke` | build report, test results, setup/MSI artifacts | Cleans generated outputs; optional live `winget` smoke tests | `dotnet`, WiX |
| `scripts/lint-scripts.ps1` | compatibility | `-Required` | analyzer console report | None beyond module import | optional PSScriptAnalyzer |
| `scripts/support/ScriptHelpers.ps1` | helper internal | dot-source only | helper functions | None by itself | PowerShell runtime |
| `scripts/support/PSScriptAnalyzerSettings.psd1` | helper internal | analyzer settings load | rule configuration | None | PSScriptAnalyzer |

Compatibility scripts are intentionally retained because existing local users and historical workflows may call them. New documentation should prefer the canonical names.
