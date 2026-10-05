# AGENTS.md

`v1.2 · 2026-10-05` — Non-derivable repository facts only. Cap ~2500 characters.

## 1. Identity & Scope
- **Purpose**: Modern C#/.NET 10 WinUI 3 desktop client for local winget package workflows and Windows Update scans.
- **Runtime / Toolchain**: .NET 10 SDK, Windows App SDK 2.3.1, PowerShell 7 / 5.1, NSIS 3.12 (x64).
- **Out of Scope**: Cloud sync services, cross-platform UI (Windows 10 1809+ x64 only).
- **Hard Constraints**: Strict Onion/Clean Architecture (`Presentation -> Application -> Domain`, `Infrastructure -> Application -> Domain`). Self-contained x64 deployment.

## 2. Verified Commands
| Workflow | Command | Shell / Cwd | Verified on | Notes / Examples |
| :--- | :--- | :--- | :--- | :--- |
| **Fast Verification** | `.\scripts\run.ps1 -Task Test -Configuration Release -NoRestore -NonInteractive` | pwsh / repo root | 2026-10-05 | 265 tests pass |
| **Format / Lint** | `.\scripts\run.ps1 -Task Lint -NonInteractive` | pwsh / repo root | 2026-10-05 | PSScriptAnalyzer (24 scripts OK) |
| **Type / Schema Check** | `.\scripts\run.ps1 -Task Typecheck -Configuration Release -NoRestore -NonInteractive` | pwsh / repo root | 2026-10-05 | Zero warnings / zero errors |
| **Build / Run / Plan** | `.\scripts\package.ps1 -NoRestore -Fast -NonInteractive` | pwsh / repo root | 2026-10-05 | NSIS setup EXE & portable ZIP |
| **Lifecycle Validation** | `powershell -ExecutionPolicy Bypass -File .\scripts\validate-installer-lifecycle.ps1 -Scope CurrentUser -SkipPackage` | pwsh / repo root | 2026-09-03 | Silent per-user install/launch/uninstall |

## 3. Architecture & Boundaries
- **Structure**: Core in `src/OnlyWinget*`, packaging in `src/OnlyWinget.Setup/OnlyWinget.nsi` via `MultiUser.nsh`.
- **Packaging & Privileges**: `OnlyWinget.exe` runs `asInvoker`. NSIS supports per-machine (`AllUsers`, `$PROGRAMFILES64`, `HKLM`) and per-user (`CurrentUser`, `$LOCALAPPDATA\Programs\OnlyWinget`, `HKCU`) via `SHCTX`.
- **Conventions**: No RESW/RESX; localization in `TextResources.cs`. Argument passing via `ArgumentList`.

## 4. Sensitive Areas & Gotchas
- **NU1004 Lockfiles**: If project RIDs change or restore fails NU1004, run `.\scripts\fix-lockfiles.ps1`.
- **Installer Validation**: `-Scope CurrentUser` runs unprivileged; `-Scope AllUsers` requires elevated PowerShell.
- **Installer ownership**: Packaging generates `InstalledFiles.nsh`; uninstall removes only its listed files and empty directories.
