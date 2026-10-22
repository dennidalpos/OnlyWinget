# Guida Visiva ai Percorsi Utente (OnlyWinget)

Questa guida illustra graficamente tutti i flussi di utilizzo di OnlyWinget per l'utente finale, descrivendo i punti di ingresso, le decisioni, le azioni, i risultati e i comportamenti in caso di errore o recupero.

---

## Indice dei Percorsi

1. [Navigazione Generale & Dashboard](#1-navigazione-generale--dashboard)
2. [Gestione dei Preset di Applicazioni](#2-gestione-dei-preset-di-applicazioni)
3. [Ricerca e Installazione Diretta di Applicazioni](#3-ricerca-e-installazione-diretta-di-applicazioni)
4. [Aggiornamenti Applicazioni (WinGet)](#4-aggiornamenti-applicazioni-winget)
5. [Windows Update (Aggiornamenti di Sistema)](#5-windows-update-aggiornamenti-di-sistema)
6. [Gestione Sorgenti Software (Repository WinGet)](#6-gestione-sorgenti-software-repository-winget)
7. [Cronologia Attività & Visualizzatore Diagnostico](#7-cronologia-attività--visualizzatore-diagnostico)
8. [Impostazioni & Personalizzazione](#8-impostazioni--personalizzazione)

---

## 1. Navigazione Generale & Dashboard

La schermata principale offre una visione d'insieme della salute del computer, delle metriche del sistema (processore, memoria, spazio su disco) e dei canali di aggiornamento.

```mermaid
flowchart TD
    Start["Avvio di OnlyWinget"] --> Home["Schermata Home / Panoramica"]
    Home --> HealthCheck{"Verifica stato del PC"}
    HealthCheck -->|"Riavvio richiesto"| AlertReboot["Mostra avviso: Riavvio del sistema in sospeso"]
    HealthCheck -->|"WinGet non disponibile"| AlertWinget["Mostra avviso: Gestore pacchetti assente o non configurato"]
    HealthCheck -->|"Tutto operativo"| StatusReady["Stato: Pronto"]
    
    AlertReboot --> Navigation{"Menu di navigazione principale"}
    AlertWinget --> Navigation
    StatusReady --> Navigation
    
    Navigation -->|"1. Pacchetti e Preset"| NavPackages["Sezione Pacchetti: Crea o gestisci le tue liste di app"]
    Navigation -->|"2. Ricerca Applicazioni"| NavSearch["Sezione Cerca: Trova nuove app nel catalogo"]
    Navigation -->|"3. Aggiornamenti"| NavUpdates["Sezione Aggiornamenti: WinGet e Windows Update"]
    Navigation -->|"4. Sorgenti Software"| NavSources["Sezione Sorgenti: Gestisci i canali di download"]
    Navigation -->|"5. Cronologia Attività"| NavActivity["Sezione Attività: Consulta storico e log"]
    Navigation -->|"6. Impostazioni"| NavSettings["Sezione Impostazioni: Lingua, tema e preferenze"]
```

---

## 2. Gestione dei Preset di Applicazioni

I preset consentono di memorizzare collezioni di programmi preferiti da installare o rimuovere in blocco (ad esempio per configurare un nuovo computer o un ambiente di lavoro).

```mermaid
flowchart TD
    StartPreset["Ingresso: Sezione Pacchetti -> Scheda Preset"] --> SelectPreset{"Scegli un Preset dal menu"}
    
    SelectPreset -->|"Crea nuovo"| NewPreset["Inserisci nome del preset"]
    NewPreset --> SaveNew["Salva preset vuoto nel catalogo"]
    
    SelectPreset -->|"Importa da file"| FilePicker["Scegli file JSON dal tuo computer"]
    FilePicker --> CheckValid{"Il file e un preset valido?"}
    CheckValid -->|"No"| ErrorImport["Mostra errore: File non supportato o corrotto"]
    CheckValid -->|"Si"| LoadImport["Carica e attiva il nuovo preset importato"]
    
    SelectPreset -->|"Usa preset esistente"| ViewPreset["Visualizza lista applicazioni del preset"]
    
    ViewPreset --> EditChoice{"Cosa vuoi fare?"}
    
    EditChoice -->|"Aggiungi app manualmente"| AddManual["Inserisci ID e sorgente dell'app"]
    AddManual --> SaveManual["Aggiungi alla lista e salva"]
    
    EditChoice -->|"Esporta preset"| ExportFile["Salva il preset come file JSON"]
    
    EditChoice -->|"Rinomina o elimina"| ManagePreset{"Rinomina o elimina?"}
    ManagePreset -->|"Rinomina"| RenameP["Inserisci nuovo nome"] --> SaveManual
    ManagePreset -->|"Elimina"| ConfirmDel{"Confermi eliminazione?"}
    ConfirmDel -->|"Si"| DeleteP["Preset rimosso dal catalogo"]
    ConfirmDel -->|"No"| ViewPreset
    
    EditChoice -->|"Installa o disinstalla"| SelectApps["Spunta le applicazioni desiderate"]
    SelectApps --> BatchAction{"Azione da eseguire"}
    BatchAction -->|"Installa"| RunInstall["Avvia installazione dei selezionati"]
    BatchAction -->|"Disinstalla"| RunUninstall["Avvia disinstallazione dei selezionati"]
    
    RunInstall --> ExecutionLoop["Esecuzione in corso con barra di avanzamento"]
    RunUninstall --> ExecutionLoop
    
    ExecutionLoop --> UserCancel{"L'utente clicca Annulla?"}
    UserCancel -->|"Si"| StopOp["Interrompe l'operazione in modo sicuro"]
    UserCancel -->|"No"| CompleteOp["Operazione terminata"]
    
    StopOp --> ShowResults["Riepilogo: mostra app riuscite, fallite o interrotte"]
    CompleteOp --> ShowResults
    ShowResults --> RetryFailed["Le app fallite restano selezionabili per riprovare"]
```

---

## 3. Ricerca e Installazione Diretta di Applicazioni

Permette di cercare programmi nell'intero catalogo di WinGet e Microsoft Store, con la libertà di installarli immediatamente sul computer oppure di salvarli in un preset.

```mermaid
flowchart TD
    StartSearch["Ingresso: Sezione Pacchetti -> Scheda Cerca"] --> InputQuery["Scrivi il nome o l'ID dell'app desiderata"]
    InputQuery --> PressSearch["Premi Cerca o Invio"]
    
    PressSearch --> QueryExecution["Ricerca nelle sorgenti abilitate"]
    QueryExecution --> HasResults{"Trovata corrispondenza?"}
    
    HasResults -->|"Nessun risultato"| EmptyResult["Mostra: Nessun pacchetto trovato"]
    EmptyResult --> Suggestion["Suggerimento: Controlla il nome o abilita altre sorgenti"]
    Suggestion --> InputQuery
    
    HasResults -->|"Risultati disponibili"| DisplayResults["Mostra elenco app con Nome, ID, Versione e Fornitore"]
    DisplayResults --> SelectItems["Seleziona una o piu app dalla lista"]
    
    SelectItems --> ActionChoice{"Cosa desideri fare?"}
    
    ActionChoice -->|"Installa subito"| DirectInstall["Clicca Installa Selezionati"]
    DirectInstall --> RunningDirect["Download e installazione diretta sul PC"]
    RunningDirect --> DirectResult{"Esito installazione"}
    DirectResult -->|"Riuscita"| SuccessDirect["App pronta all'uso! Registrata in Attività"]
    DirectResult -->|"Errore"| FailDirect["Mostra errore con dettagli e codice"]
    FailDirect --> DisplayResults
    
    ActionChoice -->|"Aggiungi a Preset"| AddToPreset["Clicca Aggiungi al Preset Attivo"]
    AddToPreset --> ConfirmAdd["App inserite nel preset corrente per utilizzi futuri"]
```

---

## 4. Aggiornamenti Applicazioni (WinGet)

Identifica i programmi installati sul PC che dispongono di una versione più recente e consente di aggiornarli singolarmente o tutti insieme.

```mermaid
flowchart TD
    StartUpdates["Ingresso: Sezione Aggiornamenti -> Scheda WinGet"] --> ScanUpdates["Scansione applicazioni installate sul PC"]
    ScanUpdates --> CheckUpdates{"Ci sono nuove versioni?"}
    
    CheckUpdates -->|"Tutto aggiornato"| NoUpdates["Messaggio: Tutte le applicazioni sono aggiornate!"]
    NoUpdates --> ManualRefresh["Possibilita di rieseguire la verifica manuale"]
    
    CheckUpdates -->|"Aggiornamenti presenti"| ListUpdates["Tabella con Versione Corrente e Nuova Versione"]
    ListUpdates --> SelectionChoice{"Scelta aggiornamenti"}
    
    SelectionChoice -->|"Tutti"| SelectAll["Seleziona tutti gli aggiornamenti"]
    SelectionChoice -->|"Personalizzata"| SelectSome["Spunta singolarmente solo le app desiderate"]
    
    SelectAll --> ClickApply["Clicca su Applica Aggiornamenti"]
    SelectSome --> ClickApply
    
    ClickApply --> UpdatingLoop["Download e aggiornamento sequenziale dei pacchetti"]
    UpdatingLoop --> CheckCancel{"Desideri annullare?"}
    CheckCancel -->|"Si: Annulla"| CancelUpdate["Arresto sicuro: completa l'app in corso e ferma le altre"]
    CheckCancel -->|"No"| FinishUpdate["Completamento del ciclo di aggiornamento"]
    
    CancelUpdate --> ResultsView["Mostra tabella esiti: Aggiornate, Fallite, Annullate"]
    FinishUpdate --> ResultsView
    
    ResultsView --> CheckAllSuccess{"Tutte riuscite con successo?"}
    CheckAllSuccess -->|"Si"| AutoRescan["Nuova scansione automatica: elenco pulito"]
    CheckAllSuccess -->|"No"| KeepResults["Conserva gli esiti e permette di riprovare quelle fallite"]
```

---

## 5. Windows Update (Aggiornamenti di Sistema)

Esegue la scansione degli aggiornamenti ufficiali di Windows (inclusi aggiornamenti di sicurezza, driver e componenti opzionali) e ne gestisce l'installazione controllata.

```mermaid
flowchart TD
    StartWU["Ingresso: Sezione Aggiornamenti -> Scheda Windows Update"] --> OptChoice{"Vuoi includere driver e opzionali?"}
    
    OptChoice -->|"Si"| EnableOptional["Attiva casella: Includi facoltativi e driver"]
    OptChoice -->|"No"| StandardScan["Mantieni solo aggiornamenti importanti e di sicurezza"]
    
    EnableOptional --> ClickScan["Clicca Verifica Aggiornamenti"]
    StandardScan --> ClickScan
    
    ClickScan --> ScanningWU["Contatto con i server di Windows Update"]
    ScanningWU --> FoundUpdates{"Rilevati aggiornamenti?"}
    
    FoundUpdates -->|"Nessuno"| AllGood["Messaggio: Il sistema operativo e aggiornato"]
    
    FoundUpdates -->|"Disponibili"| TableWU["Mostra aggiornamenti con Titolo, Codice KB, Gravità MSRC"]
    TableWU --> SelectWU["Seleziona gli aggiornamenti da applicare"]
    SelectWU --> ClickInstallWU["Clicca su Installa Aggiornamenti"]
    
    ClickInstallWU --> EulaAccept["Accettazione automatica termini di licenza verificati"]
    EulaAccept --> DownloadInstallWU["Fase di download e installazione in background"]
    
    DownloadInstallWU --> CancelWU{"Clicchi su Annulla?"}
    CancelWU -->|"Si"| RequestAbortWU["Invia richiesta di interruzione controllata al servizio Windows"]
    CancelWU -->|"No"| FinishWU["Installazione completata"]
    
    RequestAbortWU --> OutcomeWU["Riepilogo esito con stato interrotto"]
    FinishWU --> CheckReboot{"Un aggiornamento richiede il riavvio?"}
    
    CheckReboot -->|"Si"| NotifyReboot["Notifica prominente: Riavvia il PC per completare l'installazione"]
    CheckReboot -->|"No"| SystemUpdated["Aggiornamento completato con successo"]
```

---

## 6. Gestione Sorgenti Software (Repository WinGet)

Consente di visualizzare e configurare i canali da cui vengono scaricate le applicazioni (ad esempio il catalogo ufficiale WinGet o Microsoft Store).

```mermaid
flowchart TD
    StartSources["Ingresso: Sezione Sorgenti"] --> ListSources["Elenco sorgenti configurate: Nome, Indirizzo e Stato"]
    
    ListSources --> ActionFilter{"Cosa desideri fare?"}
    
    ActionFilter -->|"Filtra elenco"| TypeFilter["Cerca per nome, tipo o stato"]
    
    ActionFilter -->|"Attiva o disattiva sorgente"| ToggleSource["Clicca sull'interruttore della sorgente"]
    ToggleSource --> InstantSave["Preferenza salvata all'istante (Nessun admin richiesto)"]
    
    ActionFilter -->|"Aggiorna metadati"| UpdateMetadata["Clicca Aggiorna Sorgenti"]
    UpdateMetadata --> SyncRemote["Download indici aggiornati dai repository"]
    
    ActionFilter -->|"Aggiungi nuova sorgente"| AddPrompt["Inserisci Nome e Indirizzo URL del repository"]
    AddPrompt --> CheckAdminAdd{"L'app e avviata come amministratore?"}
    CheckAdminAdd -->|"No"| UacHintAdd["Avviso: Operazione bloccata. Riavvia OnlyWinget come amministratore"]
    CheckAdminAdd -->|"Si"| ValidateSource["Verifica validita URL e certificato"]
    ValidateSource -->|"Valido"| SourceAdded["Nuova sorgente registrata nel sistema"]
    ValidateSource -->|"Non valido"| ErrorSource["Errore: Indirizzo non raggiungibile o certificato errato"]
    
    ActionFilter -->|"Rimuovi sorgente"| SelectSource["Seleziona sorgente personalizzata da rimuovere"]
    SelectSource --> ConfirmRemove{"Confermi la rimozione della sorgente?"}
    ConfirmRemove -->|"No"| ListSources
    ConfirmRemove -->|"Si"| CheckAdminDel{"Privilegi amministratore presenti?"}
    CheckAdminDel -->|"No"| UacHintAdd
    CheckAdminDel -->|"Si"| SourceDeleted["Sorgente eliminata dal sistema"]
    
    ActionFilter -->|"Ripristina predefinite"| ConfirmReset{"Confermi ripristino sorgenti ufficiali?"}
    ConfirmReset -->|"Si con permessi admin"| ResetDone["Sorgenti ufficiali Microsoft reimpostate"]
```

---

## 7. Cronologia Attività & Visualizzatore Diagnostico

Registra ogni operazione eseguita per consentire all'utente di esaminare lo storico, verificare gli errori, copiare i dettagli o esportare i log per assistenza tecnica.

```mermaid
flowchart TD
    StartAct["Punto di ingresso: Scheda Attività o clic sul banner in alto"] --> ViewLog["Visualizzazione cronologica degli eventi"]
    
    ViewLog --> FilterAct{"Filtra eventi"}
    FilterAct -->|"Per Categoria"| CatFilter["Filtra: Preset, Ricerca, Aggiornamenti, Sorgenti, Sistema"]
    FilterAct -->|"Per Gravità"| SevFilter["Filtra: Solo Errori, Avvisi o Informazioni"]
    
    ViewLog --> SelectEntry["Clicca su un evento specifico"]
    SelectEntry --> ReadDetails["Leggi dettagli completi, orario ed eventuale codice errore"]
    ReadDetails --> CopyClip["Pulsante: Copia dettagli negli Appunti"]
    
    ViewLog --> ClearHistory["Clicca: Cancella cronologia attività"]
    ClearHistory --> UndoAvailable["Attivita nascoste con possibilita di fare Annulla"]
    
    ViewLog --> OpenViewer["Clicca: Visualizzatore Log Completi"]
    OpenViewer --> ModalViewer["Apertura finestra dedicata Visualizzatore Log"]
    
    ModalViewer --> ViewerAction{"Cosa desideri fare?"}
    ViewerAction -->|"Cerca testo"| SearchViewer["Filtro in tempo reale sui log di sistema"]
    ViewerAction -->|"Esporta su file"| ExportLogFile["Scegli percorso e salva file di log (.log/.txt)"]
    ViewerAction -->|"Svuota file log"| ConfirmClearLogs{"Confermi eliminazione file di log da disco?"}
    ConfirmClearLogs -->|"Si"| DeleteLogFiles["File di log rimossi dal computer"]
    ConfirmClearLogs -->|"No"| ModalViewer
    ViewerAction -->|"Chiudi"| CloseViewer["Ritorno alla schermata principale"]
```

---

## 8. Impostazioni & Personalizzazione

Permette di personalizzare l'esperienza d'uso, la lingua, il tema grafico e le impostazioni di sicurezza dell'applicazione.

```mermaid
flowchart TD
    StartSettings["Ingresso: Icona Impostazioni in basso a sinistra"] --> ViewSettings["Pannello di configurazione dell'applicazione"]
    
    ViewSettings --> SettingChoice{"Opzione da modificare"}
    
    SettingChoice -->|"Lingua interfaccia"| LangSelect["Scegli: Italiano oppure English"]
    LangSelect --> ApplyLang["Testi dell'interfaccia aggiornati all'istante"]
    
    SettingChoice -->|"Aspetto e Tema"| ThemeSelect["Scegli: Chiaro, Scuro oppure Segui Sistema"]
    ThemeSelect --> ApplyTheme["Palette visiva e contrasto applicati immediatamente"]
    
    SettingChoice -->|"Conferma azioni distruttive"| ConfirmToggle["Attiva/Disattiva dialoghi di conferma eliminazione"]
    
    SettingChoice -->|"Diagnostica e Log"| DiagToggle["Attiva/Disattiva registrazione diagnostica su disco"]
    DiagToggle --> LevelSelect["Scegli livello di dettaglio: Base, Informazioni, Dettagliato"]
    
    ApplyLang --> SaveSettings["Clicca Salva Impostazioni"]
    ApplyTheme --> SaveSettings
    ConfirmToggle --> SaveSettings
    LevelSelect --> SaveSettings
    
    SaveSettings --> SavedState["Preferenze memorizzate in modo permanente nel profilo utente"]
    
    ViewSettings --> ResetDefaults["Clicca: Ripristina valori predefiniti"]
    ResetDefaults --> ConfirmResetOpt{"Ripristinare tutte le impostazioni iniziali?"}
    ConfirmResetOpt -->|"Si"| RestoredDefaults["Tutte le opzioni tornano allo stato di fabbrica"]
```
