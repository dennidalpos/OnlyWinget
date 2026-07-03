# WinUI 3 Packaging & MSIX Deployment Checklist

This document provides a comprehensive checklist for packaging and distributing Windows App SDK applications using MSIX or installers.

## 1. Project Packaging Strategy (Packaged vs Unpackaged)
- [ ] **Decide on Packaging Model**:
  - **Packaged (MSIX)**: Requires an MSIX Packaging Project (`.wapproj`) or setting `<WindowsPackageType>MSIX</WindowsPackageType>` in .NET 9+. Provides sandbox, clean uninstall, and identity (API support for settings, background tasks, notifications).
  - **Unpackaged (Sparse/Non-MSIX)**: Uses `<WindowsPackageType>None</WindowsPackageType>`. Distributes as a standard EXE + assets. Requires bootstrapper deployment or WinAppSDK pre-installed on target machine. (This is **OnlyWinget**'s portable deployment strategy).

## 2. MSIX Checklist (Package.appxmanifest)
- [ ] **Package Identity**:
  - `Name`: Unique package name (Alphanumeric only).
  - `Publisher`: Match certificate publisher string (e.g. `CN=DeveloperName`).
  - `Version`: Must be structured as `Major.Minor.Build.Revision` (e.g. `1.0.2.0`).
- [ ] **Capabilities**:
  - Only request necessary capabilities. Desktop apps typically require `<rescap:Capability Name="runFullTrust"/>`.
- [ ] **Visual Assets**:
  - Provide all tile sizes (Square 150x150, 44x44, logo, splash screen) under the `Assets/` directory.

## 3. Code Signing & Certificates (Packaged)
- [ ] **Generate Test Certificate**:
  - Create self-signed certificate using PowerShell:
    ```powershell
    New-SelfSignedCertificate -Type Custom -Subject "CN=OnlyWingetDeveloper" -KeyUsage DigitalSignature -FriendlyName "OnlyWinget Signing Cert" -CertStoreLocation "Cert:\CurrentUser\My"
    ```
- [ ] **Export & Trust Certificate**:
  - Export certificate to `.pfx`.
  - Install to "Trusted People" (Persone attendibili) on the test machine to prevent `0x80080204` installation errors.
- [ ] **Sign Package**:
  - Run `signtool.exe` to sign the output MSIX package.

## 4. Compilation & Deployment
- [ ] **Solution Restore RID-neutral**:
  - NuGet restore should be RID-neutral. WinUI packaging projects target `win-x64`.
- [ ] **Self-Contained Publish**:
  - Configure `<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>` in `.csproj` so that Windows App Runtime is packaged directly with the application rather than requiring external deployment.
- [ ] **Release Command Checklist**:
  - Run package script to generate final portable ZIP and WiX setup MSI:
    ```powershell
    .\scripts\run.ps1 -Task Package -Configuration Release -NoRestore -NonInteractive
    ```
- [ ] **Verify Setup Output**:
  - Verify that the WiX Burn/MSI installer is built correctly inside `artifacts/installer/`.
