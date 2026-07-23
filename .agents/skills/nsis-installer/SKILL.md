---
name: nsis-installer
description: Official developer skill for Nullsoft Scriptable Install System (NSIS 3.x) installer scripting, Modern UI 2 (MUI2), x64 architecture targets, Windows registry uninstaller registration, and self-contained .NET 10 packaging.
---

# NSIS (Nullsoft Scriptable Install System) Installer Skill

This skill provides guidelines and specifications for NSIS 3.x installer scripting, configuration, and packaging for Windows desktop applications.

Source: [NSIS Official Documentation & Reference Manual](https://nsis.sourceforge.io/Docs/)

## 1. Core Principles & Architecture

- **Engine**: NSIS produces script-driven, low-overhead native Windows setup executables (`.exe`) without runtime dependencies.
- **Targeting**: Target **x64 only** for modern .NET 10 / Windows App SDK applications (`SetRegView 64`, `$PROGRAMFILES64`).
- **Execution Level**: `RequestExecutionLevel admin` (or `user` depending on installation scope).
- **Structure**:
  - `src/OnlyWinget.Setup/OnlyWinget.nsi`: Main script defining pages, files, shortcuts, and registry keys.
  - `scripts/package.ps1`: Automation entry point that runs `makensis.exe` against the setup script after `dotnet publish`.

## 2. Modern UI 2 (MUI2) Page Stack

Include Modern UI 2 macros (`!include "MUI2.nsh"`) for standard Fluent-aligned installer dialogs:
- **Installer Pages**:
  - `!insertmacro MUI_PAGE_WELCOME`
  - `!insertmacro MUI_PAGE_DIRECTORY`
  - `!insertmacro MUI_PAGE_INSTFILES`
  - `!insertmacro MUI_PAGE_FINISH`
- **Uninstaller Pages**:
  - `!insertmacro MUI_UNPAGE_CONFIRM`
  - `!insertmacro MUI_UNPAGE_INSTFILES`

## 3. Mandatory Setup Directives for x64 Self-Contained Apps

```nsis
!include "MUI2.nsh"
!include "x64.nsh"

Name "OnlyWinget"
OutFile "OnlyWinget-Setup-x64.exe"
InstallDir "$PROGRAMFILES64\OnlyWinget"
RequestExecutionLevel admin

Section "MainSection" SEC01
  SetRegView 64
  SetOutPath "$INSTDIR"
  
  ; Copy published self-contained output files
  File /r "publish\*.*"
  
  ; Create Start Menu Shortcuts
  CreateDirectory "$SMPROGRAMS\OnlyWinget"
  CreateShortCut "$SMPROGRAMS\OnlyWinget\OnlyWinget.lnk" "$INSTDIR\OnlyWinget.exe"
  
  ; Register Uninstaller in Windows Add/Remove Programs
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OnlyWinget" "DisplayName" "OnlyWinget"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OnlyWinget" "UninstallString" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OnlyWinget" "Publisher" "OnlyWinget"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OnlyWinget" "DisplayIcon" "$INSTDIR\OnlyWinget.exe"
SectionEnd

Section "Uninstall"
  SetRegView 64
  RMDir /r "$INSTDIR"
  RMDir /r "$SMPROGRAMS\OnlyWinget"
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OnlyWinget"
SectionEnd
```

## 4. Packaging Guardrails

1. **Self-Contained Deployment**: Ensure `WindowsAppSDKSelfContained=true` publish artifacts are completely included.
2. **No x86 / AnyCPU**: Reject legacy x86 directives or 32-bit registry paths.
3. **Silent Installation Support**: Test setup with `/S` command-line parameter to verify unattended installation capability.
4. **Clean Uninstall**: Ensure the uninstaller completely cleans up `$INSTDIR`, Start Menu shortcuts, and registry uninstall keys.
