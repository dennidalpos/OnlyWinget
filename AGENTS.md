# Istruzioni del repository: OnlyWinget

<!-- secondbrain:repository:begin -->
Direttive comuni applicate: revisione **4.0** — 2026-10-29. <!-- secondbrain:source global=7585B84CED201216722B7E6646C3D2845045FEEE7E44C889E92E68A5E1975A4A workstation=2BC7FB0217FB690B8CEAB7E3DD85AAB916BA4CF3016533BC9E1D02477CE9321B -->SecondBrain: integrazione opzionale della conoscenza e delle direttive comuni. Risolvi SECONDBRAIN_HOME dallʼambiente utente della postazione; nessun percorso personale è conservato in questo repository. Se il clone privato non è disponibile, prosegui con le istruzioni tecniche locali e segnala lʼintegrazione non configurata; non creare memorie o stub sostitutivi. Dal clone SecondBrain usa scripts/prompts.ps1 -Context -RepositoryPath <checkout-corrente> per risolvere il dossier e il suo index.md. Carica indice, contesto sintetico, memoria essenziale e proiezione task pertinente; approfondimenti e storico soltanto quando servono. Memorie private, tracker interni, backup e output operativi risiedono in SecondBrain. Conserva qui codice, guide e asset pubblicabili. Le direttive non autorizzano commit, push, cancellazioni di artefatti o task applicativi impliciti.
<!-- secondbrain:repository:end -->

Revisione specifica del progetto 2.5 — 2026-10-25. Vincoli specifici del progetto; i contenuti comuni sono gestiti separatamente.

## Contesto

- **Scopo**: Applicazione desktop Windows per la gestione dei flussi di aggiornamento da interfaccia moderna WinUI 3: preset di pacchetti `winget`, ricerca fonti, scansione e applicazione di aggiornamenti `winget` e Windows Update tramite COM, batch ripetibili di installazione e disinstallazione.
- **Modalità**: Greenfield (semplificazione aggressiva per architettura minimale; eliminazione shim temporanei e dead code)
- **Target Applicativo**: Desktop Windows 10/11 x64 (WinUI 3 / Windows App SDK) con supporto bilingue (Inglese default, Italiano).
- **Stack & Toolchain**: C# / .NET 10, WinUI 3, `Microsoft.Extensions.Hosting` (DI), Serilog, `CommunityToolkit.Mvvm`, SQLite con EF Core 10 (`onlywinget.db`), xUnit, `dotnet`, PowerShell (`pwsh`) e NSIS.

## Vincoli specifici

  * Validazione rigorosa degli input per import di preset JSON e parametri CLI `winget`.
  * Preservare file di configurazione e secret locali esistenti; isolamento database SQLite locale.
  * Architettura Clean Architecture (Onion boundaries): UI -> Application -> Domain -> Infrastructure.
  * Operazioni COM e scansioni Windows Update sempre asincrone e cancellabili con `CancellationToken`.
  * Database embedded SQLite transazionale; preservare integrità durante aggiornamenti di schema.

## Comandi e verifica

Comandi verificati staticamente contro i file del progetto; questa revisione delle direttive non certifica l'esecuzione di build o test applicativi. Consultare anche i gate della CI, quando presenti.

| Attività | Comando | Ambiente | Evidenza e limiti |
|---|---|---|---|
| **Restore** | `dotnet restore` | pwsh / root | Ripristino dipendenze NuGet |
| **Build Debug** | `dotnet build` | pwsh / root | Compilazione soluzione in Debug |
| **Targeted checks** | `dotnet test --filter "FullyQualifiedName~Target" --nologo` | pwsh / root | Test rapidi mirati all'ambito modificato |
| **Full checks** | `dotnet test --nologo && dotnet build -c Release --nologo` | pwsh / root | **Gate obbligatorio pre-commit** (test completi + build Release) |

## Riferimenti e collaborazione

- Consultare README e documentazione effettivamente presenti; versioni dai manifest/lockfile, non da ipotesi sullo stack.
- 02-areas/products/OnlyWinget/memory.md e 02-areas/products/OnlyWinget/context.md (nel dossier SecondBrain) conservano decisioni tecniche durevoli, gotcha e architettura; aggiornarli a fine task.
- Tracker autorevole nel dossier SecondBrain: `02-areas/products/OnlyWinget/tracker.json`, dichiarato in repository.json. Conservare schema JSON v1 e storico; build e test restano indipendenti da SecondBrain.
- Eseguire i controlli previsti per la modifica prima di un commit richiesto. Nessun commit o push automatico.
- Preservare dati persistenti e configurazioni esterne; non estendere la manutenzione a operazioni distruttive non autorizzate.
