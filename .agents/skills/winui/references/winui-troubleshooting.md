# OnlyWinget troubleshooting

Read the actual command output and affected source before applying a fix.

- **`NU1004` lockfile mismatch**: `global.json` pins `.NET SDK 10.0.402` (`rollForward: disable`) and `Directory.Build.props` enforces `RestorePackagesWithLockFile=true`. Regenerate all five `packages.lock.json` files with `.\scripts\fix-lockfiles.ps1`; never hand-edit lockfiles or disable `--locked-mode`.
- **XAML compiler / typecheck errors**: run `.\scripts\run.ps1 -Task Typecheck -Configuration Release -NoRestore -Full -NonInteractive` to inspect generated XAML/C# diagnostics, `x:Bind` path types and namespace visibility.
- **Locked executable during build/typecheck/clean**: another `OnlyWinget.exe` from this checkout may be running. Close it gracefully or pass `-StopRunningInstance` (note open tracker item `AUDIT-41` in `PROJECT_STATUS.json` when an inaccessible or elevated `OnlyWinget` process exists on the host).
- **Stale UI content**: verify `Mode=OneWay`/`TwoWay` on `x:Bind`, `ObservableCollection<T>` instance stability, `DispatcherQueue` marshaling in `FeatureViewModel` and `StateChanged` subscription lifetime.
- **Preset editor / save failure**: retain the user's draft; distinguish validation/mutation failure from post-mutation SQLite save failure (where fields stay locked and Apply retries only persistence).
- **Dialog or file-picker errors**: verify `ContentDialog.XamlRoot` (and inherited `RequestedTheme`), single-dialog-per-window guard, and native owner HWND (`WinRT.Interop.WindowNative.GetWindowHandle` / `InitializeWithWindow.Initialize`).
- **Launch failure**: `scripts/dev.ps1` launches `artifacts/bin/OnlyWinget/<Configuration>/net10.0-windows10.0.17763.0/win-x64/OnlyWinget.exe`. Ensure `Build` completed for that configuration; never add MSIX package identity (`WindowsPackageType` must remain `None`).
- **Diagnostic log I/O failure**: inspect `DiagnosticLogStore.LastError` and `AppSettings` log-level filters without deleting personal logs under `%LOCALAPPDATA%\OnlyWinget\logs` or clearing `Activity` history.

See [commands](../../onlywinget/references/commands.md) for verification commands.
