---
name: nsis-installer
description: Official developer skill for Nullsoft Scriptable Install System (NSIS 3.x) installer scripting, Modern UI 2 (MUI2), x64 architecture targets, Windows registry uninstaller registration, and self-contained packaging.
---

# NSIS Setup Installer Developer Skill

This skill provides guidelines and specifications for creating and maintaining Nullsoft Scriptable Install System (NSIS 3.x) setup scripts for Windows desktop applications.

Source: [NSIS Documentation & Reference](https://nsis.sourceforge.io/Docs/)

## 1. Setup Architecture

- **Architecture Target**: 64-bit Windows (`Target x64`). Set `SetCompressor /SOLID lzma`.
- **UI Framework**: Modern UI 2 (`MUI2.nsh`).
- **Registry Uninstaller**: Register under `HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall\<AppName>` with `DisplayName`, `UninstallString`, `DisplayVersion`, `Publisher`, `InstallLocation`.

## 2. Packaging Workflow

- **Atomic Staging**: Compile NSIS setup executables into staging directories before copying to `artifacts/dist/` to prevent partial file writes.
- **Uninstaller Safety**: Ensure `Uninstall` section cleans shortcut links, program files directory, and registry keys cleanly.
