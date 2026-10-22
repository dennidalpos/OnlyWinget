# Documentazione OnlyWinget

Guida e indice generale della documentazione di progetto per OnlyWinget, applicazione desktop moderna in C# / .NET 10 WinUI 3 per la gestione dei flussi di aggiornamento `winget` e Windows Update.

---

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
- **[Tracker Operativo Unico (`../PROJECT_STATUS.json`)](../PROJECT_STATUS.json)**:
  - Registro canonico dei soli task aperti/pendenti (preserva lo schema JSON `todos`). Non contiene lo storico dei task conclusi.
- **[Istruzioni Repository (`../AGENTS.md`)](../AGENTS.md)**:
  - Vincoli di progetto, gate pre-commit, comandi verificati e integrazione con il dossier SecondBrain.
- **[Skill Sviluppatore (`../.agents/skills/`)](../.agents/skills/)**:
  - 12 skill canoniche per lo sviluppo WinUI 3, test di regressione, packaging NSIS e CLI WinGet.

---

## 2. Archivio Storico delle Sessioni di Audit e Bonifica

La cronologia degli interventi di auditing e bonifica è conservata nei seguenti documenti storici:

1. **[Audit Iniziale 2026-10-05 (`audit-2026-10-05.md`)](audit-2026-10-05.md)**:
   - Censimento iniziale di 40 rilievi (AUDIT-01 .. AUDIT-40) su architettura, persistenza, sicurezza, UI, automazione e documentazione.
2. **[Bonifica Tranche 1: 2026-10-06 (`audit-remediation-2026-10-06.md`)](audit-remediation-2026-10-06.md)**:
   - Chiusura di 21 rilievi: dismissione URL protocol non sicuro, isolamento percorsi, rimozione broad-kill dei processi, validazione installer NSIS e prime correzioni CLI.
3. **[Bonifica Tranche 2: 2026-10-07 (`audit-remediation-2026-10-07.md`)](audit-remediation-2026-10-07.md)**:
   - Chiusura rilievi AUDIT-16 (diagnostica), AUDIT-17 (errori HRESULT) e censimento AUDIT-35 (concorrenza/lock UI).
4. **[Bonifica Tranche 3: 2026-10-08 (`audit-remediation-2026-10-08.md`)](audit-remediation-2026-10-08.md)**:
   - Chiusura AUDIT-25, AUDIT-26, AUDIT-28 (dismissione writer JSON e DPAPI), AUDIT-29 (branding logo), AUDIT-30 locale e centralizzazione skill in `.agents/skills`.
5. **[Revisione Tracker 2026-10-08 (`tracker-review-2026-10-08.md`)](tracker-review-2026-10-08.md)**:
   - Prima revisione sistematica del backlog residuo e allineamento formale del tracker.
6. **[Revisione Tracker 2026-10-09 (`tracker-review-2026-10-09.md`)](tracker-review-2026-10-09.md)**:
   - Qualificazione interattiva UI per AUDIT-10 (import/focus/layout) e AUDIT-27 (gestione picker e finestre native).
7. **[Revisione Tracker 2026-10-17 (`tracker-review-2026-10-17.md`)](tracker-review-2026-10-17.md)**:
   - Aggiornamento toolchain all'SDK esatto .NET `10.0.402`, blocco roll-forward, rigenerazione `packages.lock.json` e diagnostica CI.
8. **[Revisione Tracker 2026-10-18 (`tracker-review-2026-10-18.md`)](tracker-review-2026-10-18.md)**:
   - Chiusura AUDIT-19 (navigazione/bozze preset) e AUDIT-22 (log viewer/clipboard/dialoghi).
   - Qualificazione hosted su ramo fixture per AUDIT-12 (rifiuto discrepanze tag/commit e stop pubblicazione).
   - Decisione approvata dall'utente: cancellazione delle prove live su Windows di test per assenza ambiente dedicato.

---

## 3. Mappa di Sintesi dei Rilievi di Audit (AUDIT-01 .. AUDIT-42)

| Rilievo | Ambito | Stato | Evidenza di riferimento |
|---|---|---|---|
| **AUDIT-01** | Protocollo URL non sicuro | **Chiuso** (2026-10-06) | [`audit-remediation-2026-10-06.md`](audit-remediation-2026-10-06.md#audit-01--closed) |
| **AUDIT-02** | Isolamento dati utente | **Chiuso** (2026-10-06) | [`audit-remediation-2026-10-06.md`](audit-remediation-2026-10-06.md#audit-02--closed) |
| **AUDIT-03** | Installazione live WinGet | **Cancellato per decisione utente** | Windows test non disponibile ([`tracker-review-2026-10-18.md`](tracker-review-2026-10-18.md#decision--windows-test-environment-tasks-cancelled)) |
| **AUDIT-04** | Installazione live Windows Update | **Cancellato per decisione utente** | Windows test non disponibile ([`tracker-review-2026-10-18.md`](tracker-review-2026-10-18.md#decision--windows-test-environment-tasks-cancelled)) |
| **AUDIT-05** | Qualificazione WUA COM/PowerShell | **Chiuso** (2026-10-18) | Regressioni worker e rifiuto selezione ([`tracker-review-2026-10-18.md`](tracker-review-2026-10-18.md#continuation--audit-05-and-windows-update-evidence-closed)) |
| **AUDIT-06** | Gestione elevazione UAC sorgenti | **Chiuso** (2026-10-06) | [`audit-remediation-2026-10-06.md`](audit-remediation-2026-10-06.md#audit-06--closed) |
| **AUDIT-07** | Sanitizzazione argomenti CLI | **Chiuso** (2026-10-06) | [`audit-remediation-2026-10-06.md`](audit-remediation-2026-10-06.md#audit-07--closed) |
| **AUDIT-08** | Risultati parziali e cancellazione | **Chiuso** (2026-10-06) | [`audit-remediation-2026-10-06.md`](audit-remediation-2026-10-06.md#audit-08--closed) |
| **AUDIT-09** | Validazione transazionale preset | **Chiuso** (2026-10-06) | [`audit-remediation-2026-10-06.md`](audit-remediation-2026-10-06.md#audit-09--closed) |
| **AUDIT-10** | Navigazione e layout WinUI | **Chiuso** (2026-10-09) | Qualificazione UI reale ([`tracker-review-2026-10-09.md`](tracker-review-2026-10-09.md#batch-1-verification-evidence)) |
| **AUDIT-11** | Chiusura processi e ownership | **Chiuso** (2026-10-06) | [`audit-remediation-2026-10-06.md`](audit-remediation-2026-10-06.md#audit-11--closed) |
| **AUDIT-12** | Validazione release CI | **Chiuso** (2026-10-18) | Casi negativi qualificati su branch fixture ([`tracker-review-2026-10-18.md`](tracker-review-2026-10-18.md#continuation--audit-12-hosted-negative-qualification-completed)) |
| **AUDIT-13** | Concorrenza task packaging | **Chiuso** (2026-10-06) | Mutex `.package.lock` ([`audit-remediation-2026-10-06.md`](audit-remediation-2026-10-06.md#audit-13--closed)) |
| **AUDIT-14** | Rollback e promozione installer | **Chiuso** (2026-10-06) | [`audit-remediation-2026-10-06.md`](audit-remediation-2026-10-06.md#audit-14--closed) |
| **AUDIT-15** | Ownership disinstallazione NSIS | **Chiuso** (2026-10-06) | `InstalledFiles.nsh` ([`audit-remediation-2026-10-06.md`](audit-remediation-2026-10-06.md#audit-15--closed)) |
| **AUDIT-16** | DiagnosticLogStore e rotazione log | **Chiuso** (2026-10-07) | Budget 140 MiB / 14 file ([`audit-remediation-2026-10-07.md`](audit-remediation-2026-10-07.md#audit-16--closed)) |
| **AUDIT-17** | Classificazione codici errore WinGet | **Chiuso** (2026-10-07) | HRESULT mapping ([`audit-remediation-2026-10-07.md`](audit-remediation-2026-10-07.md#audit-17--closed)) |
| **AUDIT-18** | Preflight pacchetti installati | **Chiuso** (2026-10-06) | [`audit-remediation-2026-10-06.md`](audit-remediation-2026-10-06.md#audit-08--closed) |
| **AUDIT-19** | Navigazione e salvataggio preset | **Chiuso** (2026-10-18) | Qualificazione UI isolata ([`tracker-review-2026-10-18.md`](tracker-review-2026-10-18.md#continuation--audit-19-closed)) |
| **AUDIT-20** | Rescan automatico solo su successo | **Chiuso** (2026-10-06) | [`audit-remediation-2026-10-06.md`](audit-remediation-2026-10-06.md#audit-20--closed) |
| **AUDIT-21** | Ripristino preferenze sorgenti | **Chiuso** (2026-10-06) | [`audit-remediation-2026-10-06.md`](audit-remediation-2026-10-06.md#audit-21--closed) |
| **AUDIT-22** | Log viewer accessibilità e temi | **Chiuso** (2026-10-18) | Qualificazione UI isolata ([`tracker-review-2026-10-18.md`](tracker-review-2026-10-18.md#continuation--audit-22-closed)) |
| **AUDIT-23** | Disattivazione fallback automatico WU | **Chiuso** (2026-10-06) | [`audit-remediation-2026-10-06.md`](audit-remediation-2026-10-06.md#audit-23--closed) |
| **AUDIT-24** | Gestione disinstallazione pacchetti | **Chiuso** (2026-10-06) | [`audit-remediation-2026-10-06.md`](audit-remediation-2026-10-06.md#audit-24--closed) |
| **AUDIT-25** | Migrazione schema SQLite `user_version` | **Chiuso** (2026-10-08) | Backup e rimozione colonne ([`audit-remediation-2026-10-08.md`](audit-remediation-2026-10-08.md#audit-25--closed)) |
| **AUDIT-26** | Blocco salvataggio su load corrotto | **Chiuso** (2026-10-08) | [`audit-remediation-2026-10-08.md`](audit-remediation-2026-10-08.md#audit-26--closed) |
| **AUDIT-27** | Cancellazione import e finestre modal | **Chiuso** (2026-10-09) | `test-ui-dialog-ownership.ps1` ([`tracker-review-2026-10-09.md`](tracker-review-2026-10-09.md#batch-2-verification-evidence)) |
| **AUDIT-28** | Dismissione writer JSON e DPAPI | **Chiuso** (2026-10-08) | [`audit-remediation-2026-10-08.md`](audit-remediation-2026-10-08.md#audit-28--source-cleanup-closed-schema-residue-separated) |
| **AUDIT-29** | Generazione media NSIS da logo master | **Chiuso** (2026-10-08) | [`audit-remediation-2026-10-08.md`](audit-remediation-2026-10-08.md#audit-29--closed-media-remainder) |
| **AUDIT-30** | Allineamento e validazione landing | **Parzialmente chiuso / Bloccato** | Locale allineato; validazione remota bloccata su HTTP 404 ([`PROJECT_STATUS.json`](../PROJECT_STATUS.json#L6)) |
| **AUDIT-31** | Riconciliazione file installer obsoleti | **Aperto (solo sorgenti)** | Necessita payload autentico precedente ([`PROJECT_STATUS.json`](../PROJECT_STATUS.json#L5)) |
| **AUDIT-32** | Validazione parametri CLI | **Chiuso** (2026-10-06) | [`audit-remediation-2026-10-06.md`](audit-remediation-2026-10-06.md#audit-32--closed) |
| **AUDIT-33** | Timeout e gestione processi CLI | **Chiuso** (2026-10-06) | [`audit-remediation-2026-10-06.md`](audit-remediation-2026-10-06.md#audit-33--closed) |
| **AUDIT-34** | Test regressione cancellazione batch | **Chiuso** (2026-10-06) | [`audit-remediation-2026-10-06.md`](audit-remediation-2026-10-06.md#audit-34--closed) |
| **AUDIT-35** | Concorrenza e lock workflow UI | **Chiuso** (2026-10-07) | Lock unico e semaforo ([`audit-remediation-2026-10-07.md`](audit-remediation-2026-10-07.md#audit-35--closed)) |
| **AUDIT-36** | Separazione log ed errori diagnostici | **Chiuso** (2026-10-06) | [`audit-remediation-2026-10-06.md`](audit-remediation-2026-10-06.md#audit-36--closed) |
| **AUDIT-37** | Pulizia protocollo profili secondari | **In attesa di riscontro** | Profilo corrente pulito ([`PROJECT_STATUS.json`](../PROJECT_STATUS.json#L8)) |
| **AUDIT-38** | Riconciliazione dipendenze non usate | **Chiuso** (2026-10-06) | [`audit-remediation-2026-10-06.md`](audit-remediation-2026-10-06.md#audit-38--closed) |
| **AUDIT-39** | Documentazione canonica di prodotto | **Chiuso** (2026-10-06) | [`docs/architecture.md`](architecture.md), [`operations.md`](operations.md), [`release.md`](release.md) |
| **AUDIT-40** | Struttura tracker a soli task aperti | **Chiuso** (2026-10-06) | [`audit-remediation-2026-10-06.md`](audit-remediation-2026-10-06.md#audit-40--closed) |
| **AUDIT-41** | Difetto chiusura processo inaccessibile | **Aperto nel tracker** | Rilievo del 2026-10-22 su `CloseOwnedApplication.ps1` ([`PROJECT_STATUS.json`](../PROJECT_STATUS.json#L3)) |
| **AUDIT-42** | Validazione input CLI su import preset | **Aperto nel tracker** | Rilievo del 2026-10-22 su caratteri di controllo CLI ([`PROJECT_STATUS.json`](../PROJECT_STATUS.json#L4)) |

---

## 4. Evidenze e Limiti di Verifica

Per garantire la massima trasparenza tecnica, si distinguono esplicitamente le verifiche completate con successo dai comportamenti non verificabili nell'ambiente locale corrente:

### Comportamenti Verificati
- **Compilazione**: `dotnet build -c Release --nologo` passa con **0 errori e 0 avvisi** su tutta la soluzione.
- **Suite di Test**: `dotnet test --nologo` esegue **392 test unitari/integrazione passati con successo** (0 falliti, 4 smoke live esclusi per configurazione).
- **Integrità Script**: `scripts/lint.ps1` valida **40 script PowerShell su 40**.
- **Formattazione**: `scripts/format.ps1` valida il codice senza violazioni di stile.
- **Skill di Sviluppo**: `scripts/install-skills.ps1` certifica **12 skill canoniche** in `.agents/skills`.
- **Packaging e Installer**: NSIS compila regolarmente l'installer x64 e produce lo ZIP portatile self-contained; la regressione dei file posseduti preserva file non correlati.
- **Regressioni Isolate**: passano con successo le verifiche di promozione artefatti (`test-package-artifacts.ps1`), chiusura processi (`test-process-ownership.ps1`), proprietà delle finestre (`test-ui-dialog-ownership.ps1`), snapshot UIA (`test-ui-state-snapshot.ps1`), diagnostica CI (`test-check-diagnostics.ps1`), convalida release (`test-release-validation.ps1`) e generazione media (`test-media-assets.ps1`).
- **Bonifica Residui**: rimossi filesystem orfani (`skills/`, directory sorgenti vuote dismesse `Navigation`/`Security`, cache legacy `dependencies/windowsappsdk` con backup di recupero in `%TEMP%`, artefatti orfani di `WuaQualification`).

### Limiti di Verifica e Attività Non Eseguibili nel Contesto Locale
1. **Operazioni Live WinGet e Windows Update su Sistema Host**:
   - I test non scaricano né installano pacchetti software reali o aggiornamenti di sistema operativi sul computer di sviluppo per preservare lo stato del sistema (i 4 metodi con attributo `[LiveFact]` sono saltati per default).
2. **Validazione Upgrade su Sistema Operativo Dedicato**:
   - La verifica del ciclo di vita su una macchina Windows di test isolata è stata cancellata con approvazione esplicita dell'utente (`tracker-review-2026-10-18.md`), non essendo disponibile un ambiente host secondario.
3. **Sito Landing Distribuito**:
   - L'endpoint GitHub Pages pubblico (`https://dennidalpos.github.io/OnlyWinget/`) restituisce HTTP 404 in quanto il servizio non è configurato/attivato sui server GitHub. La validazione remota degli hash HTML rispetto ai binari resta pertanto bloccata (AUDIT-30).
4. **Riconciliazione File Delta Versioni Precedenti**:
   - Non è presente nel repository un pacchetto autentico precedente (1.0.0 o 1.0.1 autentico): l'archivio ZIP locale etichettato 1.0.1 contiene internamente l'assembly 1.0.2.0. La riconciliazione delle cancellazioni dell'installer resta in attesa di un payload genuino (AUDIT-31).
5. **Profili Utente Secondari per Chiave Protocollo**:
   - Nessun profilo secondario affetto dalla vecchia chiave URL `onlywinget://` è stato identificato sul sistema host; sul profilo utente corrente la chiave è assente (AUDIT-37).
6. **Finestra Vuota dopo Lungo Idle**:
   - Nessun incidente è attualmente riproducibile sul sistema e i log storici registrano solo l'innesco della mitigazione preventiva (`MainWindow.RefreshAfterLongIdle`) senza eccezioni catturate.
7. **Rilievi Aperti nel Tracker (AUDIT-41 e AUDIT-42)**:
   - I difetti relativi alla gestione di istanze con `MainModule` inaccessibile e alla validazione caratteri CLI durante l'import preset rimangono aperti e tracciati in `PROJECT_STATUS.json`.
