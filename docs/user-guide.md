# User guide

[Italian reference](user-guide.it.md)

OnlyWinget combines reusable application lists, WinGet search, application upgrades and Windows Update. The descriptions below use the Italian UI labels where they identify a control; English is available in Settings.

## Start safely

Start OnlyWinget.exe after installation or extracting the entire portable ZIP. Check the [requirements](../README.md#requirements). The app loads the local workspace and detects capabilities. Resolve loading errors before saving new data; preserve the current files when investigating migration or recovery.

Navigation contains Home, Pacchetti, Aggiornamenti, Sorgenti, Attività and Impostazioni. Search is a Packages mode; Open logs is also available from navigation. Home shows CPU, memory, disk, uptime, Windows version and connectivity; these metrics are not a complete PC-health diagnosis.

~~~mermaid
flowchart LR
    Start[Start and load workspace] --> State{Loaded?}
    State -->|No| Recover[Preserve files and inspect recovery error]
    State -->|Yes| Home[Home]
    Home --> Packages[Packages presets or search]
    Home --> Updates[WinGet or Windows Update]
    Home --> Sources[Sources]
    Home --> Activity[Activity and logs]
    Home --> Settings[Settings]
~~~

## 1. Presets

In **Pacchetti → Preset**, create a named list or select an existing one. Add packages by ID/source or from Search. Both ID and source form the identity: identical IDs from different sources can be separate entries.

Import/export uses onlywinget.preset.v1. Import validates the document and resolves packages against enabled sources; duplicate preset names are refused. Generic JSON and native WinGet export are not equivalent formats.

Add/edit/remove entries, export the list, then select packages for the intended install/uninstall action. Persistent editing and preset changes are blocked during an operation. Apply or discard a draft before changing page, mode or preset. If an accepted edit fails to save, fields remain locked and Apply retries that save; do not add the edit again.

The active preset change is automatically saved once accepted. If persistence fails, inspect the error and use Save workspace to retry.

## 2. Search and install

Open **Pacchetti → Cerca**, enter a name/ID and search using at least one enabled source. Results expose name, ID, source, version and publisher when available. Select rows to install directly or add to the active preset; direct installation does not require a preset.

Read source warnings even when other sources return usable results. Identical searches may use a five-minute in-memory cache. Inspect per-package outcomes and Activity after a change.

## 3. WinGet upgrades

Under **Aggiornamenti → WinGet**, scan, compare installed/available versions and select the rows to apply. Coverage depends on WinGet recognition and enabled sources.

~~~mermaid
flowchart LR
    Scan[Scan upgrades] --> Review[Review versions and select]
    Review --> Apply[Apply selected batch]
    Apply --> Outcome{Outcome}
    Outcome -->|Success| Rescan[Automatic rescan]
    Outcome -->|Failed or cancelled| Retain[Retain outcomes diagnostics and pending rows]
    Retain --> Inspect[Inspect installed state before retry]
~~~

Cancel requests interruption; it cannot guarantee an active native installer finishes or rolls back. Outcomes distinguish completed packages, an unconfirmed active package and packages never started. Failure/cancellation does not trigger an automatic rescan; a manual scan preserves the batch summary.

## 4. Windows Update

Under **Aggiornamenti → Windows Update**, choose software, drivers, Microsoft Update and optional-update settings separately. Initially software is selected; drivers/optional updates are excluded. Select at least software or drivers, scan and inspect KB/title/metadata before choosing updates.

Installation validates IDs/revisions for the complete selection before accepting selected EULAs, downloading and installing. Required elevation and restart warnings remain meaningful. MSRC severity is displayed when available; a detected restart field is not a prediction for every future installation.

The PowerShell fallback is for unavailable COM activation; it does not automatically retry a failed COM installation. Cancel requests native abort and cleanup, without rollback. If interruption/cleanup is unconfirmed, the app reports uncertainty. Inspect Windows Update's actual state before retrying. See [metadata limits](operations.md#windows-update-metadata).

## 5. Sources

Reload the source list to inspect WinGet configuration. Local enable/disable preferences affect searches without removing the native source.

| Action | Effect |
|---|---|
| Reload | Reads without elevation, preserving URLs and local preferences |
| Update metadata | Runs the WinGet source-update operation |
| Enable/disable | Saves a local preference without administrator rights |
| Add/remove/reset | Requires the app running elevated and changes native WinGet configuration |

Input validation does not replace WinGet's registration/access result. If a native change succeeds but local save fails, reload before retrying. Cancellation does not undo a completed change.

## 6. Activity and logs

Activity supports text/severity/category filters, detail copying and history export. Its clear/cancel actions concern activity history.

Open logs instead shows diagnostic messages with filter/copy/export/clear. Clearing requires confirmation inside the viewer; cancelling leaves logs intact. Read any write/export/delete error before retrying. Exported text can contain filesystem paths and source addresses, so review it before sharing.

## 7. Settings

Choose system/English/Italian language and system/light/dark theme, confirmations, diagnostics and log level. Preferences include continuing after an error and installer-hash bypass. Changes save automatically. Hash bypass changes WinGet verification and is off by default. Restore defaults asks for confirmation and saves the initial values again.

## Local data and help

By default both installed and portable distributions store data in LocalAppData/OnlyWinget: onlywinget.db, settings.json, source-preferences-v1.json and logs. Logs rotate at 10 MiB with at most 14 recognized files/140 MiB after successful writes.

Preserve files on load/migration failure and follow [workspace recovery](operations.md#workspace-schema-migration-and-recovery). For support provide the version, operation and sanitized error as described in the [README](../README.md#support). Offline unit tests do not qualify real application installation, WinGet privileges or Windows Update execution.
