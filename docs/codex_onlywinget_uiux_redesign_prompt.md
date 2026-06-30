# Prompt Codex — Redesign UI/UX OnlyWinget

Repository: https://github.com/dennidalpos/OnlyWinget

Obiettivo: riprogettare completamente la UI/UX di OnlyWinget in ottica greenfield, scalabile, centralizzata e manutenibile. L’app va trattata come nuova: sono consentite modifiche distruttive, rimozione di codice esistente, cambio di logica UI, riorganizzazione file/cartelle e refactor profondi, purché siano coerenti con l’architettura del repository.

## Prima di modificare

1. Leggi `AGENTS.md`.
2. Controlla `git status --short`.
3. Analizza le pagine WinUI esistenti in `src/OnlyWinget/Pages`.
4. Analizza `App.xaml`, `MainWindow.xaml`, `MainWindow.xaml.cs`, `PageUi.cs`, `TableColumnLayout.cs`, `TextResources.cs`.
5. Analizza gli stati presentation in `src/OnlyWinget.Application/Presentation`.
6. Conserva stringhe visibili inglesi e italiane, ma puoi riorganizzare il sistema di localizzazione se utile.

## Vincoli architetturali

- Mantieni dipendenze one-way:
  - WinUI Presentation -> Application -> Domain
  - Infrastructure -> Application -> Domain
- Non introdurre dipendenze non necessarie.
- Non cambiare schema workspace/preset exchange format salvo necessità esplicita.
- Mantieni WinUI 3.
- Mantieni sorgenti compatibili con .NET/Windows App SDK usati dal progetto.
- Non introdurre static refresh events.
- Mantieni item sources stabili dove possibile.
- Ogni operazione cancellabile deve usare un vero `CancellationToken`.
- Non fare commit/push se non richiesto.

## Problema da risolvere

L’attuale UI è costruita pagina per pagina. Tabelle, header, righe, action bar, padding, responsive layout, empty state, loading state, error state e command enablement sono duplicati o gestiti manualmente nei code-behind. Questo causa disallineamenti, layout incoerenti, bassa scalabilità e difficoltà nel mantenere UI/UX uniforme.

## Obiettivo finale

Passare da pagine XAML indipendenti a una UI dichiarativa guidata da modelli:

```text
Route -> ScreenModel/ViewModel -> PageScaffold -> CommandBar -> StatePresenter -> Table/Form components
```

Implementare una base UI centralizzata e scalabile prima di rifinire le singole schermate.

---

# FASE 1 — Design system interno

Crea o riorganizza una struttura simile:

```text
src/OnlyWinget/
  Shell/
  DesignSystem/
    Tokens/
    Controls/
    Tables/
    Forms/
    States/
    Commands/
  Features/
    Home/
    Packages/
    Updates/
    Sources/
    Activity/
    Settings/
  Services/
  ViewModels/
```

Se preferisci una struttura diversa, va bene, ma deve separare chiaramente:

- shell/navigation;
- componenti UI riusabili;
- feature pages;
- ViewModel;
- servizi UI.

Sposta gli stili globali fuori da `App.xaml` in dizionari separati:

- `Typography.xaml`
- `Spacing.xaml`
- `Cards.xaml`
- `Buttons.xaml`
- `Tables.xaml`
- `Forms.xaml`
- `States.xaml`

Regola: evitare numeri hardcoded ripetuti nelle pagine. Introdurre token per:

- page max width;
- page padding wide/medium/compact;
- spacing;
- card padding;
- table cell padding;
- selection column width;
- row min height;
- breakpoints.

---

# FASE 2 — PageScaffold

Crea un componente centrale, ad esempio `PageScaffold`, che gestisca:

- titolo;
- sottotitolo;
- contenuto;
- primary actions;
- secondary actions;
- destructive actions;
- loading/error/empty state;
- padding pagina;
- max width;
- responsive layout;
- scroll behavior;
- footer/status area.

Le singole pagine non devono più ridefinire manualmente:

- `MaxWidth="1440"`;
- padding `28`, `24`, `16,64,16,16`;
- `VisualStateManager` duplicati;
- action bar responsive locali;
- card layout ripetitivo.

Migra almeno Dashboard/Home, Sources e Activity al nuovo `PageScaffold`. Se possibile, migra tutte le pagine.

---

# FASE 3 — Tabella centralizzata

Rimuovi il modello attuale basato su header Grid e row Grid duplicati.

Crea un controllo unico, ad esempio `OnlyWingetTable`, con:

- definizione colonne tipizzata;
- header generato dalla stessa definizione usata per le righe;
- scroll orizzontale comune tra header e body;
- colonna selezione standard;
- supporto per checkbox;
- supporto per testo primario;
- supporto per celle selezionabili;
- supporto per tooltip su testo troncato;
- divisori coerenti;
- compact/card layout;
- keyboard navigation;
- accessibilità/Narrator;
- high contrast;
- 200% text scaling.

Crea una definizione colonne tipizzata, ad esempio:

```text
TableColumnDefinition
- Id
- HeaderResourceKey
- BindingPath
- Width
- MinWidth
- IsPrimary
- IsSelection
- IsTextSelectable
- CellTemplateKey
- TooltipBindingPath
- Alignment
- Visibility/CompactVisibility
```

Migra queste tabelle:

- Search results
- Updates
- Windows Update
- Preset packages
- Sources, se utile

Elimina o depreca `FixedTableLayout` e i layout `Column0Width`, `Column1Width`, ecc. Non devono più essere il sistema centrale.

Regola obbligatoria: una colonna deve essere dichiarata una sola volta. Header e celle devono essere generati dalla stessa definizione.

---

# FASE 4 — Command system e CommandBar

L’attuale `PresentationCommand(string Id, string ResourceKey, bool IsEnabled)` è troppo povero.

Introduci un command model più ricco, ad esempio:

```text
UiCommand
- Id tipizzato, idealmente enum o strongly typed id
- LabelResourceKey
- Icon
- Kind: Primary, Secondary, Destructive, Cancel, Navigation
- Placement
- IsEnabled
- IsVisible
- TooltipResourceKey
- Confirmation
- Shortcut opzionale
```

Crea un componente `OnlyWingetCommandBar` che:

- riceve una lista di comandi;
- ordina primary/secondary/destructive/cancel;
- gestisce overflow;
- gestisce layout compact;
- mostra loading/cancel in modo coerente;
- sostituisce le action bar manuali sparse nelle pagine.

Sostituisci i comandi stringa come `"preset.add"`, `"updates.refresh"`, `"operation.cancel"` con id tipizzati o costanti centralizzate. Evita magic string sparse.

---

# FASE 5 — StatePresenter

Crea un componente centrale `StatePresenter` per:

- empty state;
- loading state;
- executing state;
- error state;
- disabled/unavailable capability state.

Non usare più un semplice `TextBlock StatusText` come meccanismo universale.

Modelli suggeriti:

- `EmptyStateModel`
- `ErrorStateModel`
- `ProgressStateModel`
- `CapabilityUnavailableStateModel`

Ogni stato deve poter avere:

- icona;
- titolo;
- descrizione;
- azione primaria;
- azione secondaria;
- dettagli tecnici espandibili quando utile.

Migra:

- `Empty_Search`
- `Empty_Updates`
- `Empty_WindowsUpdates`
- `Empty_Sources`
- `Empty_Activity`
- `Empty_Presets`
- `Empty_Packages`

---

# FASE 6 — OperationBanner / progress

Centralizza progress/loading/cancel in un componente, ad esempio `OperationBanner`.

Deve mostrare:

- operazione corrente;
- fase;
- dettaglio pacchetto/update;
- percentuale se disponibile;
- stato indeterminato se percentuale non nota;
- pulsante cancel se disponibile;
- eventuale errore finale;
- eventuale reboot required.

Rimuovi la costruzione manuale di progress text nei code-behind delle pagine.

---

# FASE 7 — ViewModel e riduzione code-behind

Riduci drasticamente il code-behind.

Ogni pagina dovrebbe:

- inizializzare componenti;
- collegare ViewModel;
- delegare interazioni al ViewModel/command adapter;
- contenere solo logica WinUI inevitabile.

Sposta fuori dai code-behind:

- mapping stato;
- localizzazione;
- enable/disable pulsanti;
- form validation;
- progress text;
- dialoghi;
- conferme;
- file picker, tramite servizi UI;
- trasformazione righe.

Crea servizi UI:

- `DialogService`
- `FilePickerService`
- `ClipboardService`, se utile
- `NavigationService`, se utile

Mantieni il `Workflow` applicativo come fonte logica, ma la UI deve consumare modelli pronti.

---

# FASE 8 — Redesign della navigazione

Sostituisci la navigazione attuale basata su moduli tecnici con workflow più chiari:

```text
Home
Pacchetti
Aggiornamenti
Sorgenti
Attività
Impostazioni
```

Implementa un `NavigationRegistry` o equivalente con route dichiarative:

- Id;
- ResourceKey;
- Icon;
- Page/ViewModel factory;
- Visibility;
- eventuale badge/status.

Rimuovi switch manuali nel `MainWindow.xaml.cs` per testi navigation.

Aggiungi pagina Settings o scaffold iniziale, anche minimale, per:

- lingua;
- tema;
- conferme distruttive;
- diagnostica/log;
- comportamento installazioni;
- reset app/configurazione.

---

# FASE 9 — Redesign feature: Pacchetti

Unifica concettualmente Search e Presets in una feature “Pacchetti”.

Obiettivo UX:

- l’utente deve vedere preset attivo, risultati ricerca e pacchetti selezionati nello stesso workflow;
- deve essere chiaro a quale preset vengono aggiunti i pacchetti;
- deve essere chiaro cosa succede prima di installare/disinstallare.

Layout desktop suggerito:

- pannello preset a sinistra;
- area centrale con pacchetti del preset o risultati ricerca;
- pannello dettagli/azioni a destra;
- search bar globale in alto;
- tab o segmented control: “Preset”, “Ricerca”, “Risultati operazione”.

Layout compact:

- preset selector;
- search;
- lista/table/card;
- azioni sticky bottom o command bar.

Funzioni UX:

- stato “già nel preset” sui risultati search;
- azione rapida “Aggiungi” su riga;
- selezione multipla;
- preview install/uninstall;
- conferma destructive per uninstall preset;
- validazione package id/source;
- import/export come azioni, non come sezione isolata.

Se una migrazione completa è troppo ampia, prepara i componenti e migra progressivamente, ma evita di lasciare duplicazione nuova.

---

# FASE 10 — Redesign feature: Aggiornamenti

Unifica Updates e Windows Update in una feature “Aggiornamenti”.

Interfaccia suggerita:

- segmented control/tab: “App winget” e “Windows Update”;
- toolbar comune: Analizza/Aggiorna, Installa/Applica selezionati, Annulla;
- filtri comuni;
- tabella coerente;
- stato reboot required;
- errori per riga;
- riepilogo operazione.

Internamente possono restare provider separati, ma la UI deve presentarsi come un flusso unico.

---

# FASE 11 — Sources

Riprogetta Sources come gestione configurazione:

- tabella/card coerente;
- nome;
- URL/argument;
- tipo;
- stato;
- enabled;
- error details;
- azioni per riga;
- validazione add source;
- test sorgente prima di aggiungere, se possibile;
- reset defaults con conferma e preview.

Mantieni conferme per remove/reset, ma centralizzale nel nuovo DialogService/ConfirmationService.

---

# FASE 12 — Activity

Riprogetta Activity come timeline diagnostica:

- filtri per severity;
- filtri per categoria;
- ricerca;
- copia dettaglio;
- esporta log, se possibile;
- dettagli espandibili;
- clear con conferma soft o undo.

Ogni activity item dovrebbe avere categoria visuale, severity chiara, timestamp, title, message e dettagli tecnici se disponibili.

---

# FASE 13 — Home

Riprogetta Dashboard come Home operativa.

Deve rispondere a:

- winget è disponibile?
- Windows Update è disponibile?
- ci sono aggiornamenti?
- ci sono errori?
- quale preset è attivo?
- quali azioni rapide posso fare?

Elementi:

- status globale;
- quick actions;
- metriche utili;
- avvisi;
- attività recente;
- CTA chiare.

---

# FASE 14 — Forms e validazione

Crea componenti o modelli per:

- `PresetNameInput`;
- `PackageIdInput`;
- `SourceInput`;
- `ValidatedTextBox`;
- `ValidationMessage`.

I comandi devono essere disabilitati se input non valido. Mostra messaggi inline. Evita submit silenziosi di campi vuoti o duplicati.

---

# FASE 15 — Accessibilità

Ogni nuovo componente deve rispettare:

- keyboard-only navigation;
- focus state visibile;
- `AutomationProperties` coerenti;
- Narrator labels utili;
- high contrast;
- 200% text scaling;
- niente clipping critico;
- tooltip/dettagli per testo troncato;
- selection checkbox con nome contestuale.

Aggiorna eventuali note in `PROJECT_STATUS.json` solo se rimangono verifiche manuali realmente residue e azionabili.

---

# Accettazione tecnica

Alla fine:

1. Le tabelle non devono più duplicare header e row column definitions.
2. Le pagine non devono più hardcodare padding/breakpoint comuni.
3. Le action bar devono usare un componente centralizzato.
4. Empty/loading/error/progress devono usare componenti centralizzati.
5. Il code-behind deve essere fortemente ridotto.
6. La navigazione deve riflettere workflow utente, non solo moduli tecnici.
7. Le stringhe EN/IT visibili devono restare disponibili.
8. I componenti devono essere pronti per scalare a nuove pagine.
9. Non introdurre regressioni note su selezione, select-all mixed state, cancellation, progress, import/export preset, sources e Windows Update explicit scan.
10. Aggiorna o crea documentazione breve sulla nuova architettura UI.

---

# Checks

Usa gli script del repository, non comandi ad hoc, dove applicabile.

Esegui almeno:

- format;
- typecheck/build;
- test se disponibili.

Comandi indicativi:

```powershell
.\scripts\run.ps1 -Task Format -NoRestore -NonInteractive
.\scripts\run.ps1 -Task Typecheck -Configuration Release -NoRestore -NonInteractive
.\scripts\run.ps1 -Task Test -Configuration Release -NoRestore -NonInteractive
.\scripts\run.ps1 -Task Build -Configuration Release -NonInteractive
```

Se alcuni check richiedono ambiente Windows interattivo, winget, Windows Update o privilegi elevati, segnala chiaramente cosa non hai potuto verificare e perché.

---

# Handoff finale

Nel report finale includi:

- riepilogo architetturale;
- file modificati;
- componenti introdotti;
- pagine migrate;
- duplicazioni eliminate;
- check eseguiti e risultati;
- verifiche manuali residue;
- eventuali rischi o debiti tecnici rimasti;
- stato `git status --short`.
