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

The packaging task permits only one run per worktree. It currently writes final NSIS/portable outputs directly; atomic promotion remains pending under AUDIT-24.

The release workflow checks out the explicit tag, validates all four project version properties against it and runs the full `Check` gate on that commit before publication. Immediately before publication it verifies HEAD still matches the resolved SHA, the remote tag still resolves to that SHA, and both exact-version supported assets exist and are nonempty. Missing asset patterns also fail the release action.

For manual dispatch, supply an existing `vMAJOR.MINOR.PATCH` tag; a dispatch from a branch without an explicit tag fails. The selected dispatch branch cannot substitute its payload for the tag's payload. No release is published if the gate or validation fails.

Distribution is NSIS setup EXE plus self-contained portable ZIP only. The unused `PackageMsix` task, script and package manifest have been retired; no MSIX signing secret is consumed. Build-gate uploads both supported formats and retains TRX/build-report evidence.

The standalone `build-gate` workflow remains available for branch/PR verification. Its two-commit checkout supports the isolated release mismatch regression; release checkout fetches full history and tags.

Local release-contract regression:

```powershell
.\scripts\test-release-validation.ps1
```

This uses isolated local clones and sentinel assets, accepts lightweight/annotated tags, and rejects wrong checkout refs, versions, expected SHAs, missing/empty assets and remote tag drift. It creates no commits or release and runs in the full gate. The full hosted dispatch/publication flow still requires validation before shipping.

## Artifact

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
