# WinUI Implementation Plan

## Direction

OnlyWinget uses a clean WinUI 3 presentation layer on .NET 10, with Domain, Application, and Infrastructure projects kept independent from UI details.

## Foundation

- Retargeted the app and scripts to .NET 10 and Windows 10 build 17763 minimum.
- Added the stable `Microsoft.WindowsAppSDK` package version `2.2.0`.
- Added a WinUI `NavigationView` shell for Presets, Search, Updates, and Activity.
- Added new domain primitives for package identity, presets, selection, actions, operation plans, statuses, and validation.
- Added new workspace storage at `%LOCALAPPDATA%\OnlyWinget\workspace-v1.json`.
- Added current-format-only preset import/export for `onlywinget.preset.v1`.

## Remaining Work

The remaining validation and release gates are tracked in `PROJECT_STATUS.json`.
