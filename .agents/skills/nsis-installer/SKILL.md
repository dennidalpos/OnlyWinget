---
name: nsis-installer
description: "Modify or verify OnlyWinget NSIS multi-user installation, uninstall ownership and self-contained x64 artifacts."
---

# OnlyWinget NSIS installer

Use with [the project skill](../onlywinget/SKILL.md), [docs/release.md](../../../docs/release.md) and [src/OnlyWinget.Setup/OnlyWinget.nsi](../../../src/OnlyWinget.Setup/OnlyWinget.nsi).

- Keep `MultiUser.nsh`, the 64-bit registry view (`SetRegView 64`) and `SHCTX` scope: `AllUsers` installs to `$PROGRAMFILES64\OnlyWinget` and `HKLM`; `CurrentUser` installs to `$LOCALAPPDATA\Programs\OnlyWinget` and `HKCU`. The application manifest (`src/OnlyWinget/app.manifest`) remains `asInvoker`.
- `InstalledFiles.nsh` is generated from the publish directory by `scripts/support/InstallerFiles.ps1` during `scripts/package.ps1`. Regenerate it through packaging; never hand-edit it.
- Installation and uninstall share that generated ownership manifest. Uninstall deletes only distributed files and then empty directories (`RMDir`); never use recursive `RMDir /r` on `$INSTDIR` or shared shortcut directories.
- Legacy `onlywinget://` protocol cleanup (`src/OnlyWinget.Setup/LegacyProtocolCleanup.nsh` and `src/OnlyWinget.Infrastructure/System/LegacyProtocolCleanup.cs`) requires the expected label, URL marker and exact owned executable command. Preserve other copies and other users' registrations.
- Uninstall invokes `src/OnlyWinget.Setup/CloseOwnedApplication.nsh`, which runs 64-bit Windows PowerShell via `Sysnative` to request graceful closure (`CloseMainWindow`) only for `$INSTDIR\OnlyWinget.exe` and aborts before deleting files if closure fails or times out. `scripts/support/CloseOwnedApplication.ps1` ignores exited/inaccessible/null-module candidates without closing them; an unreadable path cannot establish ownership or prove the installation is unlocked.
- `scripts/support/PackageArtifacts.ps1` owns destination-volume staging, MZ/ZIP payload verification, `File.Replace` promotion, rollback backups and `.package-recovery` journal recovery. Check `02-areas/products/OnlyWinget/tracker.json (nel dossier SecondBrain)` (`AUDIT-31`, `AUDIT-37`) for open prior-release file-delta reconciliation and secondary-profile validation items.

## Verification

- Isolated NSIS ownership & protocol regression: `.\scripts\test-installer-owned-files.ps1`
- Isolated process-closure ownership regression: `.\scripts\test-process-ownership.ps1`
- Isolated staging/promotion/recovery regression: `.\scripts\test-package-artifacts.ps1`
- Host lifecycle validation (mutates machine state; `AllUsers` requires elevation; pass `-Scope`, `-SkipPackage`, `-PreviousSetupPath` directly): `.\scripts\validate-installer-lifecycle.ps1`
- Installed executable startup check: `.\scripts\validate-installed-startup.ps1 -ExePath <path>`

Sources: [NSIS manual](https://nsis.sourceforge.io/Docs/), [MultiUser](https://nsis.sourceforge.io/Docs/MultiUser/Readme.html), [File.Replace](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace?view=net-10.0).
