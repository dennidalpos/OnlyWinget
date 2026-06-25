# WinUI Migration Plan

## Direction

The WPF implementation is replaceable. The migration uses a clean WinUI 3 presentation layer on .NET 10, with Domain, Application, and Infrastructure projects kept independent from UI details.

## Completed Foundation

- Retargeted the app and scripts to .NET 10 and Windows 10 build 17763 minimum.
- Added the stable `Microsoft.WindowsAppSDK` package version `2.2.0`.
- Replaced the WPF shell with a WinUI `NavigationView` shell for Presets, Search, Updates, and Activity.
- Added new domain primitives for package identity, presets, selection, actions, operation plans, statuses, and validation.
- Added new workspace storage at `%LOCALAPPDATA%\OnlyWinget\workspace-v1.json`.
- Added current-format-only preset import/export for `onlywinget.preset.v1`.

## Remaining Work

The next implementation steps are the functional WinUI page workflows, operation execution/progress, localization, Windows App Runtime installer bootstrap, and full validation/package gates tracked in `PROJECT_STATUS.json`.
