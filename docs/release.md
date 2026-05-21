# Release and Versioning

This document defines the repository-owned release flow for OnlyWinget. It covers version
changes, release candidate verification, Git tagging, and GitHub release publishing.

## Version source of truth

The product version is declared in `src/OnlyWinget/OnlyWinget.csproj`.

Update these properties together for every release:

```xml
<Version>1.0.2</Version>
<AssemblyVersion>1.0.2.0</AssemblyVersion>
<FileVersion>1.0.2.0</FileVersion>
<InformationalVersion>1.0.2</InformationalVersion>
```

Rules:

- Use three-part SemVer-like product versions: `MAJOR.MINOR.PATCH`.
- Use the matching four-part assembly and file versions by appending `.0`.
- Use annotated Git tags named `vMAJOR.MINOR.PATCH`, for example `v1.0.2`.
- Do not publish release artifacts whose installer version, project version, and Git tag differ.
- Do not use prerelease suffixes in the WiX/MSI product version. If a prerelease build is needed,
  keep the installer version numeric and describe the prerelease state in the GitHub release title
  or notes.

`scripts/package.ps1 -Version <version>` can override the installer version for validation
scenarios, such as generating a previous-version baseline. Official releases should normally use
the version committed in `OnlyWinget.csproj`.

## Release artifacts

The user-facing installer is the unified setup executable:

```text
artifacts/dist/OnlyWinget/Release/OnlyWinget-<version>-setup.exe
```

Internal MSI files under `artifacts/dist/OnlyWinget/Release/msi/` are generated to build and
diagnose the setup bundle. They may be attached to a release for maintainer diagnostics, but the
setup EXE is the supported end-user artifact.

Include these verification artifacts in the release evidence when available:

- `artifacts/build-report.txt`
- `artifacts/test-results/**/*.trx`
- `artifacts/installer-validation/Release/installer-lifecycle-report.txt`

## Release candidate checklist

Run these steps from a clean working tree before tagging:

1. Update `src/OnlyWinget/OnlyWinget.csproj` to the target version.
2. Update user-facing docs if commands, behavior, requirements, or release notes changed.
3. Restore dependencies:

   ```powershell
   pwsh -ExecutionPolicy Bypass -File .\scripts\setup.ps1
   ```

4. Run the local verification gate:

   ```powershell
   pwsh -ExecutionPolicy Bypass -File .\scripts\check.ps1 -Configuration Release
   ```

5. Run installer lifecycle validation from elevated PowerShell on a clean or disposable Windows
   host:

   ```powershell
   pwsh -ExecutionPolicy Bypass -File .\scripts\validate-installer-lifecycle.ps1 -Configuration Release -NoRestore
   ```

6. Push the release candidate branch or commit and run the hosted GitHub Actions `build-gate`
   workflow. The hosted gate must pass before tagging.
7. Review `git status --short` and stage only intentional source, docs, and tracker changes.

## Tagging

Create the tag only after local checks, installer lifecycle validation, and hosted GitHub Actions
have passed for the exact commit being released.

```powershell
git tag -a v1.0.2 -m "OnlyWinget 1.0.2"
git push origin v1.0.2
```

If a tag is created against the wrong commit, stop and fix it before publishing. Do not publish a
release from a tag whose artifacts were not verified.

## GitHub release publishing

Publish from the GitHub release UI or an authenticated GitHub CLI session.

Required release settings:

- Tag: `vMAJOR.MINOR.PATCH`
- Title: `OnlyWinget MAJOR.MINOR.PATCH`
- Attached end-user artifact: `OnlyWinget-<version>-setup.exe`
- Release notes: summarize user-visible changes, installation notes, known issues, and verification
  evidence.

Optional maintainer attachments:

- internal x86/x64 MSI files
- `build-report.txt`
- installer lifecycle validation report
- TRX test results

After publishing:

1. Download the setup EXE from the published release page.
2. Confirm the filename version matches the tag.
3. Confirm `PROJECT_STATUS.json` no longer lists completed release-preparation work.
4. Keep any external blocker in `PROJECT_STATUS.json` only when it is still current and actionable.
