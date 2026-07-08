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

Packaging resolves `WindowsAppRuntimeInstall.exe` from an explicit `-WindowsAppRuntimeInstallerPath`, `ONLYWINGET_WINDOWS_APP_RUNTIME_INSTALLER`, local NuGet/cache paths, or the official Windows App Runtime redistributable download. Downloaded redistributables are cached in `dependencies/windowsappsdk/`, which is intentionally outside `artifacts` so cleanup does not remove reusable prerequisites.

The packaging task permits only one run per worktree and publishes the MSI and setup EXE atomically from staging. WiX 3 ICE03 is suppressed because its obsolete locale table rejects valid Windows App SDK 2.x MUI locales such as `gd-GB`, `mi-NZ`, and `ug-CN`; other linker validation remains active, apart from the existing ICE61 suppression.

Then run the hosted GitHub Actions `build-gate` workflow for the exact release commit.

## Artifact

Publish the unified setup EXE:

```text
artifacts/dist/OnlyWinget/Release/OnlyWinget-1.0.PATCH-setup.exe
```

Internal MSI files are diagnostic artifacts, not the primary end-user download.

## Tag

```powershell
git tag -a v1.0.PATCH -m "OnlyWinget 1.0.PATCH"
git push origin v1.0.PATCH
```

Publish the GitHub release from that verified tag and attach the setup EXE.
