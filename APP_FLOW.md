# APP_FLOW - OnlyWinget

## Schema generale

OnlyWinget e' una app desktop WPF Windows che orchestri preset locali e operazioni `winget`.
Non esistono backend, API server, database o autenticazione applicativa. La persistenza e' JSON locale in `%LOCALAPPDATA%\OnlyWinget\AppsList.json`; i log operativi winget finiscono in `%LOCALAPPDATA%\OnlyWinget\runtime`.

```mermaid
flowchart TD
    A[Avvio WPF App] --> B[Composizione servizi manuale]
    B --> C[Test winget --version]
    C -->|winget assente| D[Prompt install App Installer]
    D --> E[Chiusura app]
    C -->|winget presente| F[Crea MainWindow + MainViewModel]
    F --> G[Load AppsList.json]
    G --> H[UI preset/search/updates]
    F --> I[Post-startup checks async]
    I --> J[winget source update]
    J --> K[Check upgrade Microsoft.AppInstaller]
    K -->|update disponibile + conferma| L[winget upgrade/install AppInstaller]
    H --> M[Search package]
    M --> N[winget search]
    N --> O[winget show]
    O --> P[Fetch manifest GitHub raw]
    P --> Q[Dialog selezione installer]
    Q --> R[Aggiungi a preset]
    H --> S[Apply preset]
    S --> T[Install/Uninstall/Pause sequenziale]
    T --> U[winget process o runas elevato]
    U --> V[Status/log/progress UI]
    H --> W[Updates]
    W --> X[winget list --upgrade-available]
    X --> Y[Apply selected upgrades]
    Y --> Z[winget upgrade + refresh list]
```

## Flusso step-by-step

1. `App.OnStartup` crea direttamente `AppPreferencesService`, `LocalizationService`, `WingetService`, `InstallCommandBuilder`, `WingetPackageInterrogationService`, `DialogService`, `AppEntryService`, `TabService`, `OperationRunner` e `AppStartupCoordinator`.
2. `MainViewModel` calcola subito `IsWingetAvailable` con `winget --version`.
3. Se winget manca, `AppStartupCoordinator.CanContinueStartup` propone il link Microsoft Store App Installer e poi chiude l'app.
4. Se winget esiste, viene mostrata `MainWindow`; `MainViewModel.Initialize` carica preset, registra OS/elevazione e mostra la UI.
5. In background parte `RunPostStartupChecksAsync`: aggiorna sorgenti winget, controlla update di `Microsoft.AppInstaller`, opzionalmente aggiorna winget.
6. La UI principale alterna tre stati: preset workspace, search workspace, updates workspace.

## Flusso utente principale

```mermaid
flowchart LR
    A[Preset vuoto o esistente] --> B[Aggiungi/Search/Edit]
    B --> C[Interrogazione package]
    C --> D[Dialog opzioni installer]
    D --> E[Coda preset]
    E --> F[Save]
    E --> G[Apply]
    G --> H[Operazioni sequenziali]
    H --> I[Status per riga + output log]
```

- Aggiunta manuale: prompt nome, prompt id, prompt source, poi interrogazione.
- Search: `winget search --query`, selezione uno o piu' risultati, interrogazione per ogni package.
- Edit: reinterroga package esistente e riapplica selezioni valide.
- Apply: copia snapshot delle righe abilitate e processa install/uninstall/pause una alla volta.

## Flusso dati

```mermaid
flowchart TD
    A[AppsList.json] --> B[AppDataService.Load]
    B --> C[PresetWorkspaceViewModel._tabs]
    C --> D[MainWindow ListView]
    D --> E[User edits]
    E --> F[AppDataService.Save]
    F --> A
    E --> G[OperationRunner]
    G --> H[InstallCommandBuilder]
    H --> I[winget args]
    I --> J[WingetService/ElevatedWingetLauncher]
    J --> K[Output lines]
    K --> L[Status/Error/Resolution UI]
```

Persistenza:
- formato corrente: root JSON con tabs e apps.
- formato legacy non supportato: se il file non inizia con `{`, viene trattato come invalid data.
- import/export preset: file `.onlywinget.json` con preset singolo.
- salvataggio atomico: scrittura `.tmp`, `File.Replace` se il file esiste, cleanup del tmp.

## Flusso integrazioni esterne

```mermaid
sequenceDiagram
    participant UI as UI/ViewModel
    participant WS as WingetService
    participant WG as winget.exe
    participant GH as raw.githubusercontent.com
    UI->>WS: Search(query)
    WS->>WG: winget search
    WG-->>WS: tabella testo
    WS-->>UI: SearchResult[]
    UI->>WS: show --id --version?
    WS->>WG: winget show
    WG-->>WS: output localizzato
    UI->>GH: GET manifest installer YAML
    GH-->>UI: YAML o errore
    UI-->>UI: parse/normalize/default selection
```

Dipendenze reali:
- `winget.exe` e sorgenti winget.
- Microsoft Store/App Installer per aggiornamento winget.
- GitHub raw `microsoft/winget-pkgs` per manifest installer.
- WiX 3.14 bundled per packaging.

## Flusso errori/fallback

- `DispatcherUnhandledException`: mostra `Exception.Message` o `InnerException.Message`, marca handled e chiude app.
- Comandi UI asincroni: `ExecuteSafelyAsync` cattura eccezione, appende messaggio breve all'output e mostra MessageBox.
- Startup post-check: catch globale vuoto; non blocca UI ma perde diagnostica.
- Interrogazione manifest: errore GitHub/parsing -> warning e reduced mode.
- Install con no applicable installer -> retry senza selector.
- Install con manifest not found -> retry senza version.
- Upgrade con no applicable upgrade -> retry con dettagli installati/configurati.
- Package in use MSIX 0x80073D02 -> lettura mirata da log installer e mappatura ad app-in-use.

## Stati principali

- Workspace: `IsPresetWorkspaceVisible`, `IsSearchVisible`, `IsUpdatesVisible`.
- Busy: `AreMainActionsEnabled`, `IsApplyEnabled`, `AreUpdatesActionsEnabled`, `IsSearchEnabled`.
- Progress: `IsOperationProgressVisible`, `OperationProgressValue`, `OperationProgressText`.
- Shell status: `None`, `Running`, `WingetUpdating`.
- App row/update row: enabled/selected, status badge, error message, resolution.
- Package dialog: loading, reduced mode, full mode, warnings, selector states, advanced args, command preview.

## Punti fragili del flusso

- Non esiste cancellazione utente end-to-end: operazioni lunghe bloccano la UI fino a timeout o uscita processo.
- Il launcher elevato attende indefinitamente il processo UAC/elevato.
- Reduced mode permette di procedere con meno informazioni se GitHub o parser manifest falliscono.
- Output winget e YAML sono interpretati con parser testuali custom.
- File dati invalidi diventano stato default salvabile sullo stesso path.
- Preset importati possono portare argomenti avanzati `--custom`/`--override`.
- I test reali coprono solo disponibilita' winget e search, non install/update/uninstall.
