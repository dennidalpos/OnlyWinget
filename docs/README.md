# Documentazione OnlyWinget

Guida e indice generale della documentazione di progetto per OnlyWinget, applicazione desktop moderna in C# / .NET 10 WinUI 3 per la gestione dei flussi di aggiornamento `winget` e Windows Update.

---


Contenuto datato trasferito nel dossier SecondBrain: `02-areas/products/OnlyWinget/history/imported/docs/README.md`. Originale e SHA-256 nel rapporto di migrazione; questa voce conserva i riferimenti esistenti e non autorizza nuovi interventi.

## 1. Mappa della Documentazione per Ambito

La documentazione del repository è suddivisa per ambiti operativi chiaramente distinti:

### A. Documentazione Canonica di Prodotto
I documenti di riferimento primari per architettura, funzionamento, sviluppo e rilascio:

- **[Guida Visiva ai Percorsi Utente (`user-guide.md`)](user-guide.md)**:
  - Ricostruzione visiva completa dei flussi in Mermaid per ciascuna funzionalità: Home & Dashboard, Gestione Preset, Ricerca Applicazioni, Aggiornamenti WinGet, Windows Update, Gestione Sorgenti, Attività & Log, Impostazioni.
  - Percorsi decisionali, azioni, esiti e flussi di errore o recupero descritti con il linguaggio dell'utente finale.

- **[Architettura di Sistema (`architecture.md`)](architecture.md)**:
  - Confini e regole di dipendenza Onion / Clean Architecture (`UI -> Application -> Domain`, `Infrastructure -> Application -> Domain`).
  - Composizione DI e host applicativo (`AppComposition.cs` con `Microsoft.Extensions.Hosting`).
  - Persistenza SQLite embedded con Entity Framework Core 10 (`%LOCALAPPDATA%\OnlyWinget\onlywinget.db`).
  - Migrazione schema transazionale con `user_version`, backup preventivo e import dati legacy.
  - Logging diagnostico integrato Serilog / UI (`DiagnosticLogStore`) con rotazione a 10 MiB e retention a 14 file (140 MiB).
  - Interoperabilità nativa: CLI WinGet asincrona (argomenti discreti `ArgumentList`, nessun wrapper shell) e Windows Update via COM con fallback PowerShell.
  - Pattern UI: WinUI 3, `CommunityToolkit.Mvvm`, virtualizzazione tabelle `ListView` / `ItemsStackPanel`, UI Automation e localizzazione EN/IT.
  - Modello di deployment unpackaged self-contained win-x64 con setup NSIS multi-user e archivio ZIP portatile.

- **[Operazioni e Manutenzione (`operations.md`)](operations.md)**:
  - Task runner consolidato [`scripts/run.ps1`](../scripts/run.ps1) e parametri supportati (`Setup`, `Format`, `Lint`, `Typecheck`, `Test`, `Build`, `Dev`, `Package`, `Check`, `Clean`, `ValidateInstalledStartup`, `GenerateLandingSetup`).
  - Protocollo di validazione locale: check rapido vs gate obbligatorio pre-commit (`Check`).
  - Politica di pulizia output (`clean.ps1` con `-All`) e isolamento assoluto dei dati applicativi utente.
  - Gestione sorgenti WinGet, permessi di elevazione UAC, refresh e preferenze locali (`source-preferences-v1.json`).
  - Modifica preset, gestione concorrenza con lock di stato e semaforo asincrono, salvataggio cancellabile e salvaguardia bozze sporche.
  - Procedura di migrazione, backup e rollback di emergenza del database SQLite.
  - Diagnostica: sovrascrittura fixture tramite variabile d'ambiente `ONLYWINGET_DATA_ROOT`.
  - Contratti di cancellazione cooperativa (`OperationExecutionCanceledException`), gestione errori HRESULT CLI e timeout WUA.

- **[Release e Distribuzione (`release.md`)](release.md)**:
  - Politica di versionamento vincolata alla linea `1.0.x` (tag annotati `v1.0.PATCH`).
  - Gate pre-rilascio obbligatori: `Check` e `ValidateInstallerLifecycle`.
  - Requisiti del runner CI: Chocolatey NSIS `3.13.0` per compilazione installer e test di ownership.
  - Workflow GitHub Actions: `build-gate.yml` per verifiche PR/branch e `release.yml` per automazione release.
  - Packaging transazionale con mutex `artifacts/.package.lock`, directory di staging uniche, rollback a caldo e recupero promotion interrotte.
  - Chiusura sicura dei soli processi di pertinenza prima dell'installazione/disinstallazione tramite percorso verificato.

### B. Strumenti e Automazione
- **[Guida agli Script PowerShell (`../scripts/README.md`)](../scripts/README.md)**:
  - Catalogo completo dei 32 script eseguibili e degli 8 moduli di supporto sotto `scripts/support/`.
  - Bootstrapping automatico dei prerequisiti (.NET SDK da `global.json`, PSScriptAnalyzer, NSIS).
  - Gestione diagnostica e report di restore/build (`artifacts/build-report.txt`, `artifacts/check/*.log`).
  - Regole per test di ownership delle finestre native e snapshot UIA.

### C. Tracker Operativo e Direttive Agente
- **Tracker Operativo Unico (`../02-areas/products/OnlyWinget/tracker.json (nel dossier SecondBrain)`) (SecondBrain: `02-areas/products/OnlyWinget/tracker.json`)**:
  - Tracker JSON comune v1 nel dossier SecondBrain: le attivita aperte escludono `done` e `cancelled`; lo storico e la provenienza sono conservati nel dossier.
- **[Istruzioni Repository (`../AGENTS.md`)](../AGENTS.md)**:
  - Vincoli di progetto, gate pre-commit, comandi verificati e integrazione con il dossier SecondBrain.
- **[Skill Sviluppatore (`../.agents/skills/`)](../.agents/skills/)**:
  - 12 skill canoniche per lo sviluppo WinUI 3, test di regressione, packaging NSIS e CLI WinGet.

---
