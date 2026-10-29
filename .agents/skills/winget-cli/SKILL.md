---
name: winget-cli
description: "Develop OnlyWinget WinGet CLI invocation, source-aware package identity, parsing, cancellation and error classification."
---

# OnlyWinget WinGet integration

Use with [the project skill](../onlywinget/SKILL.md) and [docs/operations.md](../../../docs/operations.md). Trace callers through `src/OnlyWinget.Infrastructure/Winget/ProcessWingetCommandRunner.cs` and `src/OnlyWinget.Infrastructure/System/ProcessExternalProcessRunner.cs` before changing invocation or parsing behavior.

- Pass discrete CLI parameters via `ProcessStartInfo.ArgumentList` in `WingetCommandBuilder` with exact package ID/source selection (`--id`, `--exact`, `--source`) and `WingetInputValidator` (`src/OnlyWinget.Application/Winget/WingetInputValidator.cs`). Validate preset packages, queries, optional sources and source `add`/`remove` parameters before cache lookup or CLI calls in `PresetDocumentService`, `OnlyWingetApplication`, `WingetSourceService`, `WingetPackageSearchService` and both `WingetPackageResolver` methods. Leading option tokens and disallowed characters are rejected. Never concatenate shell commands or assume unsupported JSON output flags.
- `PackageIdentity` (`src/OnlyWinget.Domain/Packages/PackageIdentity.cs`) equality includes case-insensitive `Id` and `Source`. `WingetPackageSearchService` (5-minute cache per query/source) and `WingetPackageResolver` must preserve that composite identity.
- `OnlyWingetApplication` serializes workflows through a single operation gate; do not introduce parallel WinGet CLI execution pools.
- Read-only discovery (`search`, `list`, `upgrade`, `source list`) runs without elevation. Source `add`, `remove` and `reset` require administrator elevation; `reset` is an explicit destructive action, never an automatic certificate-failure workaround.
- `WingetOperationExecutor` preserves completed, failed, cancelled (`OperationExecutionCanceledException` carrying `Summary.Results`) and never-started batch rows. Only a fully successful apply triggers an automatic rescan; cancellation does not prove an in-flight package mutation was rolled back.
- `WingetErrorClassifier` maps native Microsoft HRESULT exit codes (signed and unsigned) ahead of localized text heuristics, even when the native code maps to `Unknown`. Deterministic failures (invalid arguments `0x8A150002`, missing/unconfigured sources `0x8A15000F`/`0x8A150015`, ambiguity, hash mismatch `0x8A150011`, certificate errors, custom installer errors `0x8A150115`) do not retry automatically.
- Preset uninstall delegates installed-package matching directly to `winget uninstall` with the original exact ID and optional source, without requiring an enabled OnlyWinget source or remote `show` lookup. For install/upgrade preflight, `IsInstalled=false` requires WinGet's native no-match result; process, source, permission, timeout or unreadable/ambiguous table outputs fail preflight with diagnostics.

## Verification

- Targeted unit tests: `dotnet test --filter "FullyQualifiedName~Winget" --nologo`
- Offline test suite: `.\scripts\run.ps1 -Task Test -Configuration Release -NoRestore -NonInteractive`
- Opt-in live read-only smoke tests (requires working host `winget`): `.\scripts\run.ps1 -Task Test -Configuration Release -RunWingetSmoke -NonInteractive`

Sources: [WinGet command documentation](https://learn.microsoft.com/en-us/windows/package-manager/winget/), [troubleshooting](https://learn.microsoft.com/en-us/windows/package-manager/winget/troubleshooting), [official return codes](https://github.com/microsoft/winget-cli/blob/master/doc/windows/package-manager/winget/returnCodes.md), [manifest schemas](https://github.com/microsoft/winget-pkgs/tree/master/doc/manifest/schema).
