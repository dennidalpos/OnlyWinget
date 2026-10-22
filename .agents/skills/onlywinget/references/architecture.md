# OnlyWinget invariants

Read [docs/architecture.md](../../../../docs/architecture.md) and [docs/operations.md](../../../../docs/operations.md) for the full system map; inspect callers before changing interfaces.

## Ownership and persistence

- `OnlyWingetApplication` (`src/OnlyWinget.Application/App/`) serializes asynchronous workflows and persistent preset edits with one operation semaphore (`operationGate`). Overlapping workflows and busy preset mutations are rejected; queued workspace saves wait asynchronously on the semaphore and honor caller cancellation. State snapshots, selections, metadata and publication share `stateLock`; no lock spans an `await`.
- `SqliteWorkspaceStore` (`src/OnlyWinget.Infrastructure/Storage/Sqlite/`) is the primary workspace store (`%LOCALAPPDATA%\OnlyWinget\onlywinget.db`, overridable per process via `ONLYWINGET_DATA_ROOT` in `AppDataPaths`). It retains the read-only migration reader for legacy `workspace-v1.json`; the legacy JSON writer and DPAPI services are retired. A failed SQLite load blocks later saves until a successful reload.
- `JsonSourcePreferenceStore` validates `source-preferences-v1.json` on load and before saving, including on fresh store instances; malformed files fail closed and remain untouched for repair/recovery.
- `WorkspaceSchemaMigration` manages SQLite `user_version=1`: unversioned databases are backed up via SQLite's native `BackupDatabase` API (`onlywinget.db.pre-schema-v1-<unique-id>.bak`; incomplete `.bak.partial` files are not valid restore points) before removing retired columns (`Description`, `CreatedAt`, `UpdatedAt`, `PackageName`) in a single transaction. Unrecognized or future schema versions fail before mutation.
- `PackageIdentity` (`src/OnlyWinget.Domain/Packages/PackageIdentity.cs`) compares both ID and source case-insensitively. Preset edits capture the original identity once. Apply succeeds only after mutation and persistence; if saving fails after an accepted mutation, fields remain locked and Apply retries only persistence. Accepted idle preset-selector changes autosave; rejected busy selections do not mutate or save.

## Native work and processes

- WinGet integration (`src/OnlyWinget.Infrastructure/Winget/`) runs via `ProcessWingetCommandRunner` and `ProcessExternalProcessRunner` using `ArgumentList` and `WingetInputValidator` (see `PROJECT_STATUS.json` for open boundary validation item `AUDIT-42`). Keep completed, failed, cancelled (`OperationExecutionCanceledException`) and never-started batch rows distinct.
- Windows Update (`src/OnlyWinget.Infrastructure/WindowsUpdate/`) uses `ComWindowsUpdateService` (`WindowsUpdateComJob`) with `BeginSearch`, `BeginDownload` and `BeginInstall` off the UI thread, `RequestAbort` and `CleanUp`. `PowerShellWindowsUpdateService` (`PowerShellWindowsUpdateJob`) is an activation fallback only—failed or cancelled COM operations never retry through PowerShell. The PowerShell fallback uses the same asynchronous job lifecycle and signals a process-scoped named event before stopping the worker; if abort/cleanup fails or the 30-second grace expires, native state is reported as uncertain. Both paths require a successful search and complete ID/revision selection coverage before accepting EULAs, and preserve `MsrcSeverity`, actual `RebootRequired` and single-prefix `KB` labels.

## Diagnostics and UI

- Serilog (`AppDiagnosticsSerilogSink`) and direct diagnostics share `DiagnosticLogStore` (`src/OnlyWinget.Infrastructure/Diagnostics/DiagnosticLogStore.cs`): one UTF-8 writer and a bounded 1000-entry memory queue filtered by `AppSettings`. Daily/numbered files (`onlywinget-yyyyMMdd.log`, `onlywinget-yyyyMMdd-NNNNNN.log`) roll at 10 MiB and retain at most 14 owned files / 140 MiB aggregate after successful writes. Unrelated files and reparse points are preserved; I/O or retention errors expose `LastError` while keeping memory entries available. Activity history clear/Undo is independent of diagnostic log clear.
- `FeatureViewModel` (`src/OnlyWinget/Presentation/FeatureViewModel.cs`) subscribes to `OnlyWingetApplication.StateChanged` and marshals updates through `DispatcherQueue`.
- `Controls/OnlyWingetTable` uses `ListView` + `ItemsStackPanel`. Dialogs opened from the app root must inherit the root's actual `ElementTheme`; `Controls/LogViewerDialog` confirms log deletion inline via `InfoBar` rather than nesting a second `ContentDialog`.

## Deployment

- The application is unpackaged self-contained `win-x64` (`WindowsPackageType=None`, `asInvoker`). NSIS (`src/OnlyWinget.Setup/OnlyWinget.nsi`) uses the generated `InstalledFiles.nsh` manifest to extract and remove only owned files and empty directories, and invokes `CloseOwnedApplication.nsh` / `LegacyProtocolCleanup.nsh`. See [docs/release.md](../../../../docs/release.md) for staged packaging, rollback and recovery.
