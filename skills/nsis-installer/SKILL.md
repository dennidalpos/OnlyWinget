---
name: nsis-installer
description: Official developer skill for Nullsoft Scriptable Install System (NSIS 3.x) installer scripting, Modern UI 2 (MUI2), x64 architecture targets, Windows registry uninstaller registration, and self-contained packaging.
---

# NSIS Setup Installer Developer Skill

This skill provides guidelines and specifications for creating and maintaining Nullsoft Scriptable Install System (NSIS 3.x) setup scripts for Windows desktop applications.

Source: [NSIS Documentation & Reference](https://nsis.sourceforge.io/Docs/)

## 1. Setup Architecture

- **Architecture Target**: 64-bit Windows (`Target x64`). Set `SetCompressor /SOLID lzma`.
- **Execution Level & Scope**: Multi-user support via `MultiUser.nsh` with `MULTIUSER_EXECUTIONLEVEL Highest` (`asInvoker` / non-admin support installing to `$LOCALAPPDATA\Programs`, and per-machine installing to `$PROGRAMFILES64`).
- **Structure**:
  - `src/OnlyWinget.Setup/OnlyWinget.nsi`: Main script defining pages, files, shortcuts, and registry keys via `SHCTX`.
  - `scripts/package.ps1`: Automation entry point that runs `makensis.exe` against the setup script after `dotnet publish`.

## 2. Modern UI 2 (MUI2) Page Stack

Include Modern UI 2 macros (`!include "MUI2.nsh"`) and `MultiUser.nsh` for standard Fluent-aligned installer dialogs:
- **Installer Pages**:
  - `!insertmacro MUI_PAGE_WELCOME`
  - `!insertmacro MUI_PAGE_LICENSE`
  - `!insertmacro MULTIUSER_PAGE_INSTALLMODE`
  - `!insertmacro MUI_PAGE_DIRECTORY`
  - `!insertmacro MUI_PAGE_INSTFILES`
  - `!insertmacro MUI_PAGE_FINISH`
- **Uninstaller Pages**:
  - `!insertmacro MUI_UNPAGE_CONFIRM`
  - `!insertmacro MUI_UNPAGE_INSTFILES`
  - `!insertmacro MUI_UNPAGE_FINISH`

## 3. Mandatory Setup Directives for x64 Self-Contained Apps

```nsis
!define MULTIUSER_EXECUTIONLEVEL Highest
!define MULTIUSER_MUI
!define MULTIUSER_INSTALLMODE_COMMANDLINE
!define MULTIUSER_USE_PROGRAMFILES64
!define MULTIUSER_INSTALLMODE_DEFAULT_REGISTRY_KEY "Software\OnlyWinget"
!define MULTIUSER_INSTALLMODE_DEFAULT_REGISTRY_VALUENAME "InstallDir"
!define MULTIUSER_INSTALLMODE_INSTDIR_REGISTRY_KEY "Software\OnlyWinget"
!define MULTIUSER_INSTALLMODE_INSTDIR_REGISTRY_VALUENAME "InstallDir"
!define MULTIUSER_INSTALLMODE_INSTDIR "OnlyWinget"

!include "MultiUser.nsh"
!include "MUI2.nsh"
!include "x64.nsh"

Name "OnlyWinget"
OutFile "OnlyWinget-Setup-x64.exe"

Function .onInit
  ${If} ${RunningX64}
    SetRegView 64
  ${EndIf}
  !insertmacro MULTIUSER_INIT
FunctionEnd

Function un.onInit
  ${If} ${RunningX64}
    SetRegView 64
  ${EndIf}
  !insertmacro MULTIUSER_UNINIT
FunctionEnd

Section "MainSection" SEC01
  ${If} ${RunningX64}
    SetRegView 64
  ${EndIf}
  SetOutPath "$INSTDIR"
  
  ; Copy published self-contained output files
  File /r "publish\*.*"
  
  ; Create Start Menu Shortcuts
  CreateDirectory "$SMPROGRAMS\OnlyWinget"
  CreateShortCut "$SMPROGRAMS\OnlyWinget\OnlyWinget.lnk" "$INSTDIR\OnlyWinget.exe"
  
  ; Register Uninstaller in Windows Add/Remove Programs (SHCTX routes to HKLM or HKCU dynamically)
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr SHCTX "Software\Microsoft\Windows\CurrentVersion\Uninstall\OnlyWinget" "DisplayName" "OnlyWinget"
  WriteRegStr SHCTX "Software\Microsoft\Windows\CurrentVersion\Uninstall\OnlyWinget" "UninstallString" "$INSTDIR\Uninstall.exe"
  WriteRegStr SHCTX "Software\Microsoft\Windows\CurrentVersion\Uninstall\OnlyWinget" "Publisher" "OnlyWinget"
  WriteRegStr SHCTX "Software\Microsoft\Windows\CurrentVersion\Uninstall\OnlyWinget" "DisplayIcon" "$INSTDIR\OnlyWinget.exe"
SectionEnd

Section "Uninstall"
  ${If} ${RunningX64}
    SetRegView 64
  ${EndIf}
  RMDir /r "$INSTDIR"
  RMDir /r "$SMPROGRAMS\OnlyWinget"
  DeleteRegKey SHCTX "Software\Microsoft\Windows\CurrentVersion\Uninstall\OnlyWinget"
SectionEnd
```
