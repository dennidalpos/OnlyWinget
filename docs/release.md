# Release

## Version

The repository is locked to the `1.0.x` release line until explicitly changed. For a patch release, update `src/OnlyWinget/OnlyWinget.csproj`:

```xml
<Version>1.0.PATCH</Version>
<AssemblyVersion>1.0.PATCH.0</AssemblyVersion>
<FileVersion>1.0.PATCH.0</FileVersion>
<InformationalVersion>1.0.PATCH</InformationalVersion>
```

Use annotated tags named `v1.0.PATCH`.

## Required Checks

From a clean working tree:

```powershell
.\scripts\run.ps1 -Task Check -Configuration Release -NonInteractive
.\scripts\run.ps1 -Task ValidateInstallerLifecycle -Configuration Release -NoRestore -NonInteractive
```

The default lifecycle run validates clean install/uninstall only and reports upgrade as not_run. For a real upgrade, use the lifecycle script directly with `-PreviousSetupPath` pointing to a genuine earlier release and optional `-PreviousVersion` to assert its installed version. `-PreviousVersion` alone fails before machine changes; rebuilding the current payload under an older label is no longer supported as upgrade evidence. AUDIT-31 still requires a true older payload with removed/renamed files and ownership reconciliation.

The packaging task permits only one run per worktree. Setup and portable outputs are built under unique staging directories on the destination volume, then validated before promotion. The setup must be a nonempty MZ executable; every ZIP file is checked against the published payload by path, length and SHA-256, including hidden files. Each final file is replaced individually with `File.Replace` (or moved on first publication), while a flushed journal and verified backups retain the previous pair for rollback.

A failed promotion restores the prior pair; the next packaging invocation recovers an interrupted promotion before rebuilding. Corrupt journals/backups fail closed and remain for inspection. The two filenames do not form one atomic filesystem transaction: readers can briefly observe different generations between replacements, so consume artifacts after packaging exits successfully. Process interruption is covered by the regression; hardware/power-loss behavior is not qualified. Interrupted staging directories can remain as generated evidence; ordinary successful/failed runs remove their own staging files.

The release workflow checks out the explicit tag, validates all four project version properties against it and runs the full `Check` gate on that commit before publication. Immediately before publication it verifies HEAD still matches the resolved SHA, the remote tag still resolves to that SHA, and both exact-version supported assets exist and are nonempty. Missing asset patterns also fail the release action.

For manual dispatch, supply an existing `vMAJOR.MINOR.PATCH` tag; a dispatch from a branch without an explicit tag fails. The selected dispatch branch cannot substitute its payload for the tag's payload. No release is published if the gate or validation fails.

Distribution is NSIS setup EXE plus self-contained portable ZIP only. The unused `PackageMsix` task, script and package manifest have been retired; no MSIX signing secret is consumed. Build-gate uploads both supported formats and retains TRX/build-report evidence.

The standalone `build-gate` workflow remains available for branch/PR verification. Its two-commit checkout supports the isolated release mismatch regression; release checkout fetches full history and tags.

Local release-contract regression:

```powershell
.\scripts\test-release-validation.ps1
```

This uses isolated local clones and sentinel assets, accepts lightweight/annotated tags, and rejects wrong checkout refs, versions, expected SHAs, missing/empty assets and remote tag drift. It creates no commits or release and runs in the full gate. The full hosted dispatch/publication flow still requires validation before shipping.

`scripts/test-package-artifacts.ps1` runs the production artifact helpers against isolated sentinels and a minimal real NSIS fixture. It covers compiler/compression/validation/promotion failures, child-process interruption and recovery, corrupt journals/backups, hidden payload files and first publication. The full `Check` gate includes this regression.

## Artifact

Uninstall requests graceful closure only for `$INSTDIR\OnlyWinget.exe`, using the embedded PowerShell helper. It waits up to five seconds per owned process; a refused close or ownership-query error exits nonzero before file/registry removal. Other executable copies are preserved. The 32-bit NSIS bootstrap invokes 64-bit Windows PowerShell through `Sysnative`; no process-name kill or forced termination is used. `scripts/test-process-ownership.ps1` validates the production macro on isolated windows, including another running portable copy and a refused close.

Publish the unified setup EXE and portable ZIP:

```text
artifacts/dist/OnlyWinget/Release/OnlyWinget-1.0.PATCH-setup.exe
artifacts/dist/OnlyWinget/Release/OnlyWinget-1.0.PATCH-portable-x64.zip
```

## Tag

```powershell
git tag -a v1.0.PATCH -m "OnlyWinget 1.0.PATCH"
git push origin v1.0.PATCH
```

Publish the GitHub release from that verified tag and attach the setup EXE and portable ZIP.
