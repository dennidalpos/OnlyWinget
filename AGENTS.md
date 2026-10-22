# Istruzioni del repository: OnlyWinget

<!-- secondbrain:repository:begin -->
Direttive comuni applicate: revisione **3.0** — 2026-10-19.
<!-- secondbrain:source global=DC59C089490D9EE3E7E7F389FCC087D199EB12C16ADD2D20EA78076D9BB3BBDB workstation=3090606D3A18890581CDAD79BE8A65348612B40AAF40D5EAEB4E62AE0215D430 -->
Dossier SecondBrain di questo repository: 02-areas/products/OnlyWinget, relativo alla radice di SecondBrain indicata nelle istruzioni globali.
- Leggi a inizio task: questo AGENTS.md, 02-areas/products/OnlyWinget/context.md, 02-areas/products/OnlyWinget/memory.md e PROJECT_STATUS.json (o 02-areas/products/OnlyWinget/tracker.md).
- Nei task di scrittura aggiorna solo novità pertinenti: decisioni e gotcha in 02-areas/products/OnlyWinget/memory.md; architettura e stato in 02-areas/products/OnlyWinget/context.md; attività nel tracker preservandone schema e storico.
- Nei task di scrittura pulisci temporanei del task e residui confermati, preservando dati e lavoro unico; rivedi diff e git status --ignored. Esegui secondbrain.ps1 -Sync dopo modifiche a dossier o direttive, oppure su richiesta. Nei task di sola lettura segnala i residui senza rimuoverli.
<!-- secondbrain:repository:end -->

Revisione specifica del progetto 2.4 — 2026-10-18. Vincoli specifici del progetto; i contenuti comuni sono gestiti separatamente.

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
- PROJECT_STATUS.json è il tracker esistente: contiene lavoro aperto, senza storico dei task conclusi; preservarne lo schema.
- Eseguire i controlli previsti per la modifica prima di un commit richiesto. Nessun commit o push automatico.
- Preservare dati persistenti e configurazioni esterne; non estendere la manutenzione a operazioni distruttive non autorizzate.
