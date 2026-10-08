# OnlyWinget invariants

Read [canonical architecture](../../../../docs/architecture.md) for the component map; inspect callers before changing interfaces.

## Ownership and persistence

Application serializes asynchronous workflows and persistent edits with one operation gate. Busy edits reject; queued saves wait with cancellation. State snapshots, selections, metadata and publication share stateLock; no lock spans await.

SQLite is the primary workspace. Preserve legacy workspace JSON during migration and retained load-failure diagnostics. A failed SQLite load blocks later saves until a successful reload. Source-preference malformed/schema-invalid files fail loading and are revalidated before saving, including on a fresh store instance; restore/repair the preserved file to recover.

The full JSON workspace writer and dormant DPAPI services are retired; SQLite retains the legacy JSON reader. Do not delete retained legacy/secret files. WorkspaceSchemaMigration owns SQLite user_version=1: legacy metadata columns are retired after a verified native backup, with transactional rollback. Preserve pre-schema-v1 backups and failed partial snapshots; restore only with all database clients closed. Future/unrecognized schemas fail before mutation.

Package identity includes both case-insensitive ID and source. An edit captures the original identity once. Apply succeeds after mutation and persistence; failed saving after accepted mutation retries only saving and keeps fields locked. Accepted idle selector changes autosave; rejected busy choices do not mutate/save. Actual draft/navigation interaction remains AUDIT-19.

## Native work and processes

WinGet uses the existing CLI runner/cache/parser stack. Use ArgumentList and forward cancellation. Keep completed batch outcomes, failures, cancellations and never-started rows distinct.

Windows Update uses BeginSearch/BeginDownload/BeginInstall jobs off the UI thread, RequestAbort and CleanUp. Cancellation or failed installation must not trigger an automatic fallback/retry. Match exact update ID/revision and complete selection coverage; preserve actual MsrcSeverity/RebootRequired and canonical KB labels. Real download/install qualification remains pending.

## Diagnostics and UI

Serilog and direct diagnostics share DiagnosticLogStore: one UTF-8 writer and bounded 1000-entry queue, filtered by the same settings. Daily/numbered files roll at 10 MiB and successful writes retain at most 14 owned files/140 MiB aggregate. I/O/retention errors preserve memory entries and expose LastError. Clear recognizes daily/rolled names; unrelated files/reparse points are preserved. Activity clear/Undo is independent.

FeatureViewModel uses the instance workflow StateChanged event and dispatcher. Use the existing field-based generators/typed commands; messenger/RelayCommand use is not universal.

OnlyWingetTable is ListView/ItemsStackPanel. Keep stable collections, theme resources and EN/IT TextResources/Localize. Log clear uses inline confirmation in the existing ContentDialog. Real picker/dialog/High Contrast checks are not proven by compilation.

## Deployment

The app is unpackaged self-contained win-x64/asInvoker. NSIS owns its generated file list and removes only distributed files/empty directories. Protocol activation is retired; composition/uninstall remove only a matching legacy HKCU handler. See [release.md](../../../../docs/release.md) for staged packaging, rollback and recovery.
