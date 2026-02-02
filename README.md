# OnlyWinget

Applicazione WPF per gestire un elenco di applicazioni Windows tramite **winget**, con salvataggio su file JSON, operazioni batch e gestione di schede. È pensata per creare liste di software da installare, aggiornare o disinstallare rapidamente, con una UI semplice e un log integrato delle operazioni.

## Requisiti

- Windows 10/11 con ambiente grafico
- .NET 8 Desktop Runtime o successivo
- `winget` disponibile nel sistema (App Installer)
- Accesso a Internet per installare/aggiornare pacchetti tramite winget

All'avvio l'app verifica la presenza di `winget` e si chiude con un messaggio di errore se non è disponibile.

## Avvio

1. Compila la soluzione `OnlyWinget.sln` con Visual Studio o `dotnet build` su Windows.
2. Esegui l'eseguibile generato oppure avvia il progetto WPF.

### Build da riga di comando (Windows)

```bash
dotnet build OnlyWinget.sln -c Release
```

L'eseguibile si trova nella cartella `bin/Release/net8.0-windows` del progetto WPF.

## Funzionalità

- Lista applicazioni con azione selezionabile: Install, Uninstall, Pause.
- Esecuzione batch di installazione/aggiornamento/disinstallazione.
- Ricerca integrata su winget con selezione rapida dell'ID.
- Gestione schede per organizzare gruppi di app.
- Salvataggio e caricamento della lista su `AppsList.json`.
- Sezione aggiornamenti con selezione dei pacchetti da aggiornare.

## Guida rapida all'uso

### Gestire le schede

- **Nuova**: crea una nuova scheda (tab) per separare gruppi di applicazioni.
- **Rinomina**: rinomina la scheda corrente.
- **Rimuovi**: elimina la scheda corrente (non è possibile eliminare l'unica scheda esistente).

### Aggiungere o modificare un'app

1. Premi **Aggiungi** per inserire una nuova app.
2. Inserisci un nome descrittivo.
3. Inserisci l'ID winget (es. `Microsoft.VisualStudioCode`).
4. Scegli l'azione desiderata (Install/Uninstall/Pause) nella colonna **Azione**.

> L'app verifica l'ID su winget e segnala eventuali duplicati.

### Ricerca winget

1. Premi **Cerca App**.
2. Digita un termine e premi **Invio** o **Cerca App**.
3. Seleziona un risultato dalla lista: l'ID verrà compilato automaticamente.
4. Premi **Usa questo ID** per aggiungerlo alla lista.

### Esecuzione delle operazioni

- **Installa/Aggiorna**: avvia la procedura batch sulle app della scheda corrente.
  - Per azione **Install**, l'app tenta prima l'upgrade; se non disponibile, esegue l'installazione.
  - Per azione **Uninstall**, esegue la disinstallazione.
  - Per azione **Pause**, salta l'app.
- Il log delle operazioni appare nel pannello inferiore.

### Aggiornamenti disponibili

1. Premi **Aggiornamenti**.
2. Seleziona i pacchetti da aggiornare.
3. Avvia **Applica aggiornamenti**.

## Struttura progetto

```
.
├── OnlyWinget.sln
└── src
    └── OnlyWinget
        ├── App.xaml
        ├── MainWindow.xaml
        ├── Commands
        ├── Models
        ├── Services
        └── ViewModels
```

### Componenti principali

- **MainWindow.xaml**: layout WPF con overlay di ricerca e aggiornamenti.
- **MainViewModel**: logica applicativa, comandi e stato UI in MVVM.
- **WingetService**: invocazione dei comandi winget e parsing dell'output.
- **AppDataService**: import/export di `AppsList.json` e migrazione dal vecchio formato.
- **LocalizationService**: dizionario stringhe IT/EN con default basato sulla cultura UI.

## Formato dati (AppsList.json)

```json
{
  "Tabs": [
    {
      "Name": "Default",
      "Apps": [
        {
          "Name": "Visual Studio Code",
          "Id": "Microsoft.VisualStudioCode",
          "Action": "Install"
        }
      ]
    }
  ]
}
```

- **Name**: nome visualizzato in lista.
- **Id**: ID winget.
- **Action**: `Install`, `Uninstall` o `Pause`.

### Migrazione dal formato legacy

Se `AppsList.json` contiene un array semplice (vecchio formato), l'app lo importa automaticamente e lo salva nel nuovo formato a schede.

## Troubleshooting

### Winget non trovato

- Assicurati che **App Installer** sia installato e aggiornato dallo Store Microsoft.
- Verifica l'output di `winget --version` da prompt.

### Nessun risultato in ricerca o aggiornamenti

- Verifica la connessione a Internet.
- Assicurati che le origini winget siano disponibili (`winget source list`).

### Errori durante installazione/upgrade

- Alcuni pacchetti richiedono privilegi amministrativi.
- Controlla il log nel riquadro inferiore per i dettagli degli errori.

## Note operative

- La funzione **Aggiornamenti** usa `winget upgrade` per ottenere i pacchetti aggiornabili.
- Le operazioni batch aggiornano lo stato delle app direttamente nella lista.
- L'output dei comandi viene normalizzato per rimuovere righe di progresso ridondanti.
