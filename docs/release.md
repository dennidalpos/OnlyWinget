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

The packaging task permits only one run per worktree and compiles the NSIS setup EXE and self-contained portable ZIP atomically from staging.

Then run the hosted GitHub Actions `build-gate` workflow for the exact release commit.

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
