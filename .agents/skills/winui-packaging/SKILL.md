---
name: winui-packaging
description: "Prepare or validate OnlyWinget self-contained NSIS setup and portable ZIP artifacts and release gates."
---

# OnlyWinget packaging and release

Use with [the project skill](../onlywinget/SKILL.md), [docs/release.md](../../../docs/release.md), [docs/operations.md](../../../docs/operations.md) and [the NSIS skill](../nsis-installer/SKILL.md).

- OnlyWinget distributes two `win-x64` self-contained artifacts under `artifacts/dist/OnlyWinget/<Configuration>/`: `OnlyWinget-1.0.PATCH-setup.exe` (NSIS multi-user installer) and `OnlyWinget-1.0.PATCH-portable-x64.zip`. MSIX packaging, signing certificates and Store submission are not used.
- Preserve the `1.0.x` version policy across `Version`, `AssemblyVersion`, `FileVersion` and `InformationalVersion` in `src/OnlyWinget/OnlyWinget.csproj` and annotated tags `v1.0.PATCH`.
- `.\scripts\run.ps1 -Task Package -Configuration Release -NoRestore -NonInteractive` acquires the per-worktree mutex `artifacts/.package.lock`, publishes self-contained `win-x64`, generates `InstalledFiles.nsh` (`scripts/support/InstallerFiles.ps1`), stages both artifacts on the destination volume (`scripts/support/PackageArtifacts.ps1`), verifies the setup MZ header and every ZIP entry (including hidden files) against the publish directory by path, length and SHA-256, and promotes each file with `File.Replace`, rollback backups and `.package-recovery` journaling.
- Interrupted promotions are recovered automatically at the start of the next `Package` run; corrupt recovery journals or backups fail closed and must be preserved for inspection. Consume the artifact pair only after `Package` exits with code 0.
- Landing page downloads (`landing/build/`) and links are aligned from built Release artifacts via `.\scripts\run.ps1 -Task GenerateLandingSetup -Configuration Release -NoRestore -NonInteractive` (`scripts/support/LandingArtifacts.ps1`). Branding icons and NSIS bitmaps (`HeaderBanner.bmp` 150x57, `WelcomeDialog.bmp` 164x314, 24-bit) are generated from `assets/logos/logo.png` via `.\scripts\update-media-assets.ps1` and `.\scripts\align-logos.ps1`.
- `.github/workflows/build-gate.yml` and `.github/workflows/release.yml` provision Chocolatey `nsis` `3.13.0` (`makensis.exe /VERSION` -> `v3.13`), run `.\scripts\run.ps1 -Task Check -Configuration Release -NonInteractive`, and (`release.yml`) verify tag/commit/version/asset invariants via `.\scripts\validate-release.ps1` before and after the gate.

## Verification

- Package artifacts: `.\scripts\run.ps1 -Task Package -Configuration Release -NoRestore -NonInteractive`
- Isolated staging/rollback/recovery regression: `.\scripts\test-package-artifacts.ps1`
- Isolated NSIS ownership & protocol regression: `.\scripts\test-installer-owned-files.ps1`
- Isolated release tag/SHA/asset regression: `.\scripts\test-release-validation.ps1`
- Isolated landing & media regressions: `.\scripts\test-landing-artifacts.ps1` and `.\scripts\test-media-assets.ps1`

Read [source-generator patterns](references/sourcegen-patterns.md) only when modifying code-generation or interop paths (`IsTrimmable=true` / analyzer compatibility).

Sources: [Microsoft deployment models](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/), [NSIS manual](https://nsis.sourceforge.io/Docs/).
