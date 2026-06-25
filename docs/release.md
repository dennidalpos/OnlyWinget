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
.\scripts\check.ps1 -Configuration Release
.\scripts\validate-installer-lifecycle.ps1 -Configuration Release -NoRestore
```

Packaging requires `WindowsAppRuntimeInstall.exe` matching the app's `Microsoft.WindowsAppSDK` version. Provide it with `-WindowsAppRuntimeInstallerPath` or `ONLYWINGET_WINDOWS_APP_RUNTIME_INSTALLER` when the file is not already available in the local NuGet cache.

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
