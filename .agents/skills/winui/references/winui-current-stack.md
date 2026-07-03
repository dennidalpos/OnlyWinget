# WinUI 3 & Windows App SDK Current Stack Reference

Last Updated: July 3, 2026

This document lists the recommended stable versions and dependencies for modern WinUI 3 / Windows App SDK desktop development, aligned with the project targets of **OnlyWinget**.

## 1. Operating System & Platform Targets
- **Target OS**: Windows 10, version 1809 (Build 17763) or higher.
- **Minimum OS version**: `10.0.17763.0`
- **Target OS version**: `10.0.17763.0`
- **Architecture**: `x64` (ARM64 supported where needed; `x86` and `AnyCPU` are obsolete/deprecated for this codebase).

## 2. Recommended .NET SDK
- **Runtime**: .NET 10.0 LTS (Current SDK version used in project: `10.0.301` / runtime version: `10.0.9`).
- **Roll Forward Policy**: `latestFeature` (configured in `global.json`).

## 3. Core SDKs and Build Tools
- **Microsoft.WindowsAppSDK**: `2.2.0` (Stable)
  - Provides the WinUI 3 controls, app lifecycle management, windowing APIs, and resource management.
  - Used in self-contained mode (`<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>`).
- **Microsoft.Windows.SDK.BuildTools**: `10.0.28000.2270` (Stable)
  - Provides the MSBuild targets to compile XAML files and generate projections.
- **Microsoft.Windows.CsWinRT**: Built-in or referenced (used for C#/WinRT interop).

## 4. UI & MVVM Libraries
- **CommunityToolkit.Mvvm**: `8.4.2` (Stable)
  - The standard MVVM framework for Windows. Extensively uses C# Source Generators (`[ObservableProperty]`, `[RelayCommand]`, `ObservableObject`).
- **CommunityToolkit.WinUI Packages**: `8.2.251219` (Stable)
  - Controls, helpers, and behaviors for WinUI 3. Avoid previews (`8.3.x-preview`) unless explicitly requested.

## 5. Development Environment Workloads
To build this project, Visual Studio 2022 (v17.10+ / MSBuild 18+) or Visual Studio Build Tools 2022 is required with:
- **.NET Desktop Development** workload (`Microsoft.VisualStudio.Workload.NetWeb` or `Microsoft.VisualStudio.Workload.ManagedDesktop`).
- **Universal Windows Platform Development** workload (`Microsoft.VisualStudio.Workload.Universal`) to install the Windows 10 SDK (10.0.17763.0) and packaging tools.
- **C++ Desktop Development** workload (required for compiling packaging/installer components).

## 6. Official Resources & References
- [Windows App SDK Release Notes](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/stable-channel)
- [NuGet Gallery - Microsoft.WindowsAppSDK](https://www.nuget.org/packages/Microsoft.WindowsAppSDK)
- [GitHub - Microsoft WindowsAppSDK Repository](https://github.com/microsoft/WindowsAppSDK)
- [Microsoft Learn - WinUI 3 Hub](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/)
- [Community Toolkit for Windows documentation](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/)
