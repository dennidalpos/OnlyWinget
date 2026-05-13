# AUDIT_CRITICO - OnlyWinget

## Executive summary

Giudizio severo: il progetto e' tecnicamente ben piu' curato di un prototipo, ma non e' ancora robusto quanto serve per una app che orchestra installer e operazioni winget su macchine Windows reali.

I check locali passano: build Release con warning come errori, format verify, 173 unit test, 2 smoke test winget reali, packaging MSI/bundle e analisi PowerShell. Questo e' positivo, ma non basta: i rischi principali sono nei failure mode runtime, non nella compilazione.

Problemi piu' seri:
- operazioni winget/elevate non cancellabili, con timeout diretto di 4 ore e attesa elevata senza timeout;
- preset importati possono persistere ed eseguire `--custom`/`--override`;
- file dati invalidi/legacy degradano a stato default salvabile sullo stesso path;
- parser custom su output winget/YAML manifest fragili;
- integrazione GitHub raw senza policy forte di timeout/retry/cache/pinning;
- copertura test reale incompleta per install/update/uninstall/elevation.

## Stack rilevato

- OS target: Windows.
- App: WPF desktop, `net8.0-windows`, `UseWPF=true`, output `WinExe`.
- SDK: `global.json` richiede `9.0.100` con rollForward `latestFeature`; ambiente audit usa .NET SDK `9.0.314`.
- Package manager: NuGet/MSBuild con `packages.lock.json` e `RestorePackagesWithLockFile=true`.
- Test: xUnit, `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`.
- Packaging: WiX 3.14 binaries vendorizzati in `tools/wix314-binaries`, MSI x86/x64 e bundle Burn.
- CI/CD: GitHub Actions `build-gate` su `windows-latest`.
- Persistenza: JSON locale in `%LOCALAPPDATA%\OnlyWinget\AppsList.json`.
- Log/runtime: `%LOCALAPPDATA%\OnlyWinget\runtime`.
- Database: assente.
- Backend/API server: assenti.
- Auth/authz applicativa: assente; solo distinzione processo standard/admin e UAC via `runas`.
- Integrazioni esterne: `winget.exe`, sorgenti winget, Microsoft Store/App Installer, `raw.githubusercontent.com/microsoft/winget-pkgs`.

## Comandi eseguiti e risultati

| Comando | Risultato |
| --- | --- |
| `git status --short` | pulito prima dell'audit |
| `dotnet restore .\OnlyWinget.sln --locked-mode` | passato; progetti aggiornati |
| `dotnet format .\OnlyWinget.sln --verify-no-changes --no-restore` | passato |
| `.\scripts\build.ps1 -Configuration Release -WarnAsError -NoRestore` | passato; 0 warning, 0 errori |
| `dotnet test .\tests\OnlyWinget.Tests\OnlyWinget.Tests.csproj -c Release --no-restore --results-directory .\artifacts\test-results --logger "trx;LogFileName=audit-unit-tests.trx"` | passato; 173/173 |
| `.\scripts\agents\analyze-scripts.ps1` | passato; tutti gli script OK |
| `dotnet list .\OnlyWinget.sln package --vulnerable --include-transitive` | passato; nessun pacchetto vulnerabile segnalato da NuGet |
| `dotnet list .\OnlyWinget.sln package --deprecated` | passato con rilievo; `xunit 2.9.3` deprecato/Legacy nel progetto test |
| `$env:ONLYWINGET_RUN_WINGET_SMOKE='1'; dotnet test ... --filter "Category=Smoke"` | passato; 2/2 smoke winget reali |
| `.\scripts\package.ps1 -Configuration Release -NoRestore -Architecture All` | passato; generati MSI x86/x64 e setup unificato `OnlyWinget-1.0.2-setup.exe` |

## Problemi ordinati per gravita'

### High

**AUD-001 - Operazioni winget senza cancellazione utente e con attesa elevata potenzialmente infinita**

- Categoria: Robustness/UX
- Evidenza: `src/OnlyWinget/ViewModels/MainViewModel.cs:598-717`, `src/OnlyWinget/Services/OperationRunner.cs:263-290`, `src/OnlyWinget/Services/ElevatedWingetLauncher.cs:29-65`, `src/OnlyWinget/Services/WingetService.cs:37-424`.
- Descrizione: apply/update disabilitano la UI e aspettano operazioni `Task.Run`; winget diretto ha timeout default di 4 ore; launcher elevato usa `WaitForExit()` senza timeout.
- Scenario: installer interattivo bloccato, UAC lasciato aperto, rete lenta, processo winget appeso.
- Impatto: app apparentemente congelata, nessun annullamento, recovery non controllata.
- Correzione: cancellation token end-to-end, pulsante annulla, timeout per tipo comando, stato cancellato/timeout e test dedicati.

**AUD-002 - Preset importati possono persistere ed eseguire argomenti winget arbitrari**

- Categoria: Security
- Evidenza: `src/OnlyWinget/Dialogs/PackageInterrogationDialog.xaml:215-247`, `src/OnlyWinget/Services/InstallCommandBuilder.cs:53-61`, `src/OnlyWinget/Services/AppDataService.cs:372-393` e `422-442`.
- Descrizione: `AdditionalCustomArgs` e `OverrideArgs` passano da UI/import/salvataggio a `winget install --custom/--override`.
- Scenario: preset `.onlywinget.json` ricevuto da terzi contiene override per cambiare comportamento installer.
- Impatto: non e' shell injection, ma e' comunque esecuzione di opzioni arbitrarie verso installer esterni.
- Correzione: trust boundary per preset importati, conferma forte, review obbligatoria, validazione/limitazione argomenti.

**AUD-003 - Un file dati invalido o legacy viene sostituito da uno stato default salvabile**

- Categoria: Data Integrity
- Evidenza: `src/OnlyWinget/Services/AppDataService.cs:64-122`, `src/OnlyWinget/ViewModels/PresetWorkspaceViewModel.cs:151-169`, `405-435`, `548-569`.
- Descrizione: dati invalidi/legacy producono scheda Default; il successivo Save scrive sullo stesso file.
- Scenario: utente apre app dopo corruzione parziale, vede warning, poi salva.
- Impatto: possibile perdita dell'ultima copia recuperabile dei preset.
- Correzione: blocco save dopo load invalido, backup permanente, recovery/migrazione legacy.

**AUD-004 - Parser manuali per output winget e YAML manifest fragili rispetto a localizzazione e schema**

- Categoria: Logic/Integration
- Evidenza: `src/OnlyWinget/Services/WingetPackageInterrogationService.cs:238-452`, `684-719`, `src/OnlyWinget/Services/WingetService.cs:516-545`.
- Descrizione: parsing basato su regex/righe/indentazione e su poche frasi inglesi/italiane.
- Scenario: winget cambia output, sistema in lingua diversa, manifest YAML usa forma non prevista.
- Impatto: reduced mode, selezioni installer errate, fallback impropri.
- Correzione: parser YAML reale o API strutturate, fixture reali da winget-pkgs, test multilingua.

### Medium

**AUD-005 - Fetch manifest GitHub senza policy esplicita di timeout, retry, cache o pinning**

- Categoria: External Integrations
- Evidenza: `src/OnlyWinget/App.xaml.cs:27-35`, `src/OnlyWinget/Services/WingetPackageInterrogationService.cs:103-143`, `219-235`.
- Descrizione: URL raw su branch `master`, `HttpClient` manuale, no cancellation token, no cache, no max size, fallback reduced mode.
- Impatto: UX lenta, scelte incoerenti con sorgente/versione, affidabilita' dipendente da GitHub.
- Correzione: HttpClient configurato, retry limitato, cache package/version, max response size, cancellation.

**AUD-006 - ViewModel e servizi centrali troppo grandi e accoppiati**

- Categoria: Architecture/Maintainability
- Evidenza: `MainViewModel.cs` 1222 righe, `WingetService.cs` 1038, `WingetPackageInterrogationService.cs` 925, `MainWindow.xaml` 951; composizione manuale in `App.xaml.cs:23-35`.
- Impatto: alta probabilita' di regressioni trasversali, test con stub complessi, layering non netto.
- Correzione: estrarre workflow/use case, gateway winget, parser e policy errori.

**AUD-007 - Errori importanti sono mostrati come testo grezzo o soppressi senza log diagnostico**

- Categoria: Observability/Error Handling
- Evidenza: `src/OnlyWinget/App.xaml.cs:65-77`, `src/OnlyWinget/ViewModels/MainViewModel.cs:1198-1208`, `src/OnlyWinget/Services/AppStartupCoordinator.cs:49-111`, `src/OnlyWinget/Services/DialogService.cs:113-140`.
- Impatto: debug produzione debole, messaggi tecnici/sensibili in UI, errori post-startup invisibili.
- Correzione: log applicativo strutturato, messaggi utente normalizzati, stack trace in diagnostica locale protetta.

**AUD-008 - Installer MSI nascosto in ARP e uninstall ricorsivo basato su registry**

- Categoria: Packaging/Operations
- Evidenza: `src/OnlyWinget.Setup/OnlyWinget.Setup.wxs:20-35`, `53-59`, `87-99`.
- Descrizione: `ARPSYSTEMCOMPONENT=1`; `RemoveFolderEx` cancella ricorsivamente il path da registry.
- Impatto: uninstall meno trasparente; path registry errato amplia il rischio cancellazione.
- Correzione: validare path sotto ProgramFiles/OnlyWinget, documentare canale uninstall, evitare ricorsione se non necessaria.

**AUD-009 - package.ps1 contiene una funzione di delete ricorsivo senza guardia di repository**

- Categoria: DevEx/Scripts
- Evidenza: `scripts/package.ps1:112-122`.
- Impatto: oggi limitato da variabili interne, ma refactor o path errato possono cancellare directory inattese.
- Correzione: riusare guardie stile `Remove-GateGeneratedPath` di `build-gate.ps1`.

**AUD-010 - Smoke test winget disabilitati risultano passed invece che skipped**

- Categoria: Testing
- Evidenza: `tests/OnlyWinget.Tests/WingetSmokeTests.cs:15-45`, `scripts/internal/build-gate.ps1:103-117`, `.github/workflows/build-gate.yml:10-16`.
- Impatto: CI verde anche quando smoke non ha realmente verificato winget.
- Correzione: skip esplicito o progetto test separato; smoke periodico/manuale obbligatorio per release.

**AUD-011 - Copertura funzionale reale incompleta su install/update/uninstall/elevation**

- Categoria: Testing/Coverage
- Evidenza: smoke reali coprono solo `TestAvailable` e search `Microsoft.PowerToys`; operation runner e winget sono prevalentemente stubbati.
- Impatto: regressioni su UAC, installer interattivi, processi in uso e rollback emergono solo su macchine utenti.
- Correzione: VM Windows pulita, test lifecycle controllati, checklist release manuale.

### Low

**AUD-012 - Dipendenza test xunit 2.9.3 deprecata**

- Categoria: Dependencies
- Evidenza: `dotnet list package --deprecated` segnala `xunit 2.9.3` come Legacy nel progetto test.
- Impatto: debito non runtime, ma da pianificare.
- Correzione: migrazione xUnit v3 o decisione esplicita di restare su v2.

## Dubbi e perplessita'

- Dubbio: il prodotto sembra pensato per utenti tecnici, ma consente operazioni installer potenti con feedback limitato su fiducia dei preset.
- Dubbio: reduced mode e' pragmatico, ma puo' mascherare problemi di manifest e portare a installazioni meno determinate.
- Dubbio: il setup unificato passa, ma l'uninstall MSI nascosto + `RemoveFolderEx` merita test in VM su upgrade/downgrade/uninstall.
- Perplessita': molti problemi sono gestiti come stringhe localizzate o output CLI, non come contratti strutturati.
- Perplessita': i test UI/layout sono presenti, ma non sostituiscono una verifica manuale di accessibilita', focus, keyboard e reader.

## Gap analysis

### Aree non coperte

- Installazione, upgrade e uninstall reali in ambiente isolato.
- UAC/elevazione reale e annullamento prompt.
- Recovery dopo processi winget bloccati.
- Migrazione dati legacy.
- Test offline/GitHub down.
- Test con sistemi non italiano/inglese.
- Test accessibilita' assistiva reale.

### Aree coperte male

- Smoke winget: solo availability/search e disabilitato per default in CI.
- Parser manifest: buone unit isolate, ma manca corpus ampio di manifest reali complessi.
- Packaging: build passa, ma mancano install/uninstall/upgrade lifecycle verificati in VM nel gate standard.
- Error handling: molte eccezioni diventano messaggi brevi senza codice diagnostico.

### Aree ambigue

- Policy di fiducia dei preset importati.
- Limiti accettabili per timeout/cancellazione.
- Comportamento atteso quando manifest GitHub non e' disponibile.
- Target lingue supportate oltre italiano/inglese.
- Se l'app deve essere sicura per utenti non tecnici.

### Assunzioni pericolose

- `winget` risponde sempre in formato tabellare prevedibile.
- GitHub raw `master` rappresenta sempre il manifest corretto per la versione vista da winget.
- L'utente capisce le implicazioni di `--override`.
- Un warning e' sufficiente prima di salvare dopo load dati invalido.
- Il processo elevato terminera' sempre.

### Domande aperte

1. I preset possono arrivare da fonti non fidate?
2. Quale UX e' richiesta per annullare un batch in corso?
3. Quanto deve durare al massimo un'installazione prima di timeout?
4. Reduced mode deve consentire silent install o solo interactive?
5. Esiste una base utenti con file legacy?
6. Il bundle Burn e' l'unico uninstall supportato?
7. Devono essere supportate lingue di sistema diverse da IT/EN?
8. Serve un log diagnostico persistente separato dall'output UI?
9. Quali pacchetti si possono usare in test lifecycle reali?
10. L'app deve funzionare offline?

### Cose da verificare manualmente

- Install/uninstall/upgrade in VM Windows pulita.
- UAC accettato, rifiutato e lasciato aperto.
- Installer interattivo che non termina.
- File `AppsList.json` corrotto e recupero utente.
- Import preset con argomenti avanzati.
- Screen reader/focus/tab order.
- Uninstall bundle/MSI e cleanup cartelle.
- Comportamento con GitHub bloccato o rete lenta.

### Funzionalita' apparentemente previste ma incomplete

- Smoke/e2e reali: esiste gating, ma e' disabilitato di default e molto limitato.
- Diagnostica: output UI e log winget ci sono, ma manca log applicativo strutturato.
- Sicurezza preset: import/export esiste, ma manca modello di fiducia.
- Recovery dati: fallback esiste, ma non un percorso robusto di recupero/migrazione.

## Raccomandazioni prioritarie

1. Cancellation/timeout end-to-end.
2. Trust boundary per preset importati e argomenti avanzati.
3. Protezione dati dopo load invalido.
4. Parser manifest/output robusti.
5. GitHub integration con timeout/retry/cache.
6. Log diagnostico applicativo.
7. Refactor dei workflow fuori da MainViewModel.
8. Test lifecycle in VM.
9. Hardening packaging/delete.
10. Migrazione o decisione su xUnit v3.

## Quick wins

- Rendere smoke test skipped quando disabilitati.
- Aggiungere guardia safe delete a `scripts/package.ps1`.
- Documentare in README/docs che `--custom`/`--override` sono potenti e da usare solo con preset fidati.
- Aggiungere backup permanente prima del primo save dopo `InvalidData`.
- Aggiungere timeout esplicito breve a fetch manifest GitHub.
- Registrare catch post-startup in output/log invece di sopprimerlo.

## Rischi sistemici

- L'app gestisce tool esterni non deterministici (`winget`, installer, UAC, rete) con un modello ancora troppo sincrono e UI-centrico.
- Il dominio e' operationally risky: un singolo argomento o manifest errato puo' cambiare installazioni reali.
- Il successo dei test unitari non dimostra affidabilita' sui failure mode piu' importanti.
- La mancanza di cancellazione e recovery degrada pesantemente l'esperienza su macchine non pulite.

## Classificazione finale

**Stato progetto: Rischioso**

Motivazione sintetica: il codice compila, i test passano e il packaging funziona, ma i flussi critici dipendono da processi esterni, rete, parser testuali e UAC senza sufficiente cancellazione, recovery, trust boundary e copertura reale. Non e' bloccante per uso controllato, ma non lo classificherei solido per produzione generalizzata.

## Top 10 problemi da risolvere prima

1. Mancanza di cancellazione/timeout per operazioni winget/elevate.
2. Esecuzione di `--custom`/`--override` da preset importati senza modello di fiducia.
3. Rischio overwrite dati dopo load invalido/legacy.
4. Parser YAML/output winget custom e fragili.
5. Fetch GitHub raw senza timeout/retry/cache/pinning.
6. Errori post-startup soppressi.
7. Log diagnostico applicativo insufficiente.
8. Test reali install/update/uninstall/elevation assenti.
9. Uninstall packaging con `RemoveFolderEx` ricorsivo da registry.
10. Smoke test disabilitati conteggiati come passati.

## Top 10 domande da chiarire col proprietario del progetto

1. I preset sono un formato di scambio fidato o non fidato?
2. Reduced mode deve permettere installazioni silent?
3. Quali timeout sono accettabili per search/show/install/update?
4. Serve annullamento per singolo pacchetto o solo batch?
5. L'app deve funzionare offline o con GitHub bloccato?
6. Quali lingue di sistema vanno supportate?
7. Esistono dati legacy da migrare?
8. Quale canale uninstall deve vedere l'utente?
9. Quale ambiente VM puo' eseguire test distruttivi controllati?
10. Quali dati possono finire nei log diagnostici?
