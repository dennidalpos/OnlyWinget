---
name: winui-development
description: Regola da attivare quando si scrive codice XAML, C#, configurazioni packageappxmanifest o si eseguono comandi di build WinUI 3.
---

# WinUI 3 Development Skill

Questa skill fornisce linee guida, best-practice e regole architetturali per lo sviluppo di applicazioni desktop Windows moderne utilizzando Windows App SDK, WinUI 3 e .NET 10 LTS, con focus sul progetto OnlyWinget.

## Attivazione e Ambito
Questa skill deve essere attivata automaticamente ogni volta che:
- Si creano, modificano o analizzano file XAML (`.xaml`).
- Si scrive codice sorgente C# (`.cs`) relativo all'interfaccia utente o al ciclo di vita di WinUI 3.
- Si configurano manifesti dell'applicazione, come `package.appxmanifest` o `app.manifest`.
- Si eseguono o modificano script di build, compilazione e packaging WinUI 3 (`scripts/run.ps1`, `MSBuild`, `dotnet build`).

---

## Regole di Sviluppo WinUI 3 & XAML

### 1. Gestione dei Controlli e Layout XAML
- **x:Bind**: Utilizzare preferibilmente `x:Bind` fortemente tipizzato al posto di `Binding` per migliorare le performance e abilitare il controllo dei tipi a tempo di compilazione.
- **x:Phase**: Utilizzare `x:Phase` nelle celle e nei template di liste complesse per ottimizzare il rendering progressivo.
- **Resource Dictionary**: Centralizzare gli stili comuni nel Design System (es. `src/OnlyWinget/DesignSystem/Resources/`).
- **Disposizioni**: Liberare esplicitamente le risorse e rimuovere gli event handler nei metodi `Unloaded` dei controlli per evitare memory leak.

### 2. Architettura MVVM & CommunityToolkit
- Utilizzare il **CommunityToolkit.Mvvm** per l'implementazione del pattern MVVM.
- **ObservableProperty**: Usare gli attributi `[ObservableProperty]` sui campi privaten per generare automaticamente le proprietà notificabili tramite Source Generators.
- **RelayCommand**: Usare `[RelayCommand]` per esporre comandi asincroni o sincroni ai controlli XAML.
- **Stato dell'Applicazione**: Non utilizzare eventi statici per propagare cambiamenti di stato. Usare invece l'evento istanziato `OnlyWingetApplication.StateChanged` per notificare le modifiche di stato.

### 3. Ciclo di Vita e Concurrency
- **Asincronia**: Serializzare le operazioni asincrone a livello di `OnlyWingetApplication`. La disabilitazione dei pulsanti nella UI non è una barriera di concorrenza sufficiente per il livello applicativo.
- **CancellationToken**: Passare sempre un `CancellationToken` valido a ciascuna operazione asincrona cancellabile. Non avviare mai attività asincrone con `CancellationToken.None` se sono destinate a essere cancellabili.
- **Threading**: Aggiornare la UI esclusivamente sul thread principale usando il dispatcher (`DispatcherQueue.TryEnqueue`).

### 4. Flusso delle Dipendenze
Le dipendenze devono essere rigorosamente unidirezionali:
```text
WinUI Presentation -> Application -> Domain
Infrastructure -----> Application -> Domain
```
- Non fare mai riferimento a tipi di Presentation o Infrastructure all'interno del Domain o dell'Application.
- Centralizzare la verifica dei requisiti di sistema (disponibilità di API, processi di terze parti, PowerShell, winget e Windows Update) nell'interfaccia `ISystemCapabilityService`.

### 5. Windows Update & Elevazione
- Le scansioni di Windows Update devono avvenire esclusivamente a seguito di un'azione esplicita dell'utente.
- La sola scoperta (read-only) delle funzionalità e degli aggiornamenti non deve richiedere privilegi di amministratore (elevation).

---

## Linee Guida di Compilazione e Rilascio
- **Piattaforma**: Solo architettura **x64**. Non introdurre codice per x86 o AnyCPU.
- **Ripristino**: Mantenere il ripristino di NuGet neutrale rispetto al RID a livello di soluzione. La selezione di `win-x64` deve essere definita nei progetti WinUI e negli script di packaging.
- **Installer**: La distribuzione avviene tramite WiX Burn/MSI (in `src/OnlyWinget.Setup`) o tramite pacchetto portable autocontenuto (ZIP) generato tramite `scripts/package.ps1`.
- **Stringhe**: Tutte le stringhe visibili all'utente devono essere localizzate in **Inglese** e **Italiano** e preservate correttamente.
