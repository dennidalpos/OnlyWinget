# Release

## Version

Update `src/OnlyWinget/OnlyWinget.csproj`:

```xml
<Version>MAJOR.MINOR.PATCH</Version>
<AssemblyVersion>MAJOR.MINOR.PATCH.0</AssemblyVersion>
<FileVersion>MAJOR.MINOR.PATCH.0</FileVersion>
<InformationalVersion>MAJOR.MINOR.PATCH</InformationalVersion>
```

Use annotated tags named `vMAJOR.MINOR.PATCH`.

## Required Checks

From a clean working tree:

```powershell
.\scripts\run.ps1 -Task Check -Configuration Release -NonInteractive
.\scripts\run.ps1 -Task ValidateInstallerLifecycle -Configuration Release -NoRestore -NonInteractive
```

Packaging resolves `WindowsAppRuntimeInstall.exe` from an explicit `-WindowsAppRuntimeInstallerPath`, `ONLYWINGET_WINDOWS_APP_RUNTIME_INSTALLER`, local NuGet/cache paths, or the official Windows App Runtime redistributable download.

Then run the hosted GitHub Actions `build-gate` workflow for the exact release commit.

## Artifact

Publish the unified setup EXE:

```text
artifacts/dist/OnlyWinget/Release/OnlyWinget-<version>-setup.exe
```

Internal MSI files are diagnostic artifacts, not the primary end-user download.

## Tag

```powershell
git tag -a vMAJOR.MINOR.PATCH -m "OnlyWinget MAJOR.MINOR.PATCH"
git push origin vMAJOR.MINOR.PATCH
```

Publish the GitHub release from that verified tag and attach the setup EXE.
