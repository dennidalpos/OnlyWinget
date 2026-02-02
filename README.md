# OnlyWinget

Applicazione WPF per gestire un elenco di applicazioni Windows tramite **winget**, con salvataggio su file JSON, operazioni batch e gestione di schede.

## Requisiti

- Windows 10/11 con ambiente grafico
- .NET 8 Desktop Runtime o successivo
- `winget` disponibile nel sistema (App Installer)

All'avvio l'app verifica la presenza di `winget` e si chiude con un messaggio di errore se non è disponibile.

## Avvio

1. Compila la soluzione `OnlyWinget.sln` con Visual Studio o `dotnet build` su Windows.
2. Esegui l'eseguibile generato oppure avvia il progetto WPF.

## Funzionalità

- Lista applicazioni con azione selezionabile: Install, Uninstall, Pause.
- Esecuzione batch di installazione/aggiornamento/disinstallazione.
- Ricerca integrata su winget con selezione rapida dell'ID.
- Gestione schede per organizzare gruppi di app.
- Salvataggio e caricamento della lista su `AppsList.json`.
- Sezione aggiornamenti con selezione dei pacchetti da aggiornare.

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

## Note operative

- La funzione **Aggiornamenti** usa `winget upgrade` per ottenere i pacchetti aggiornabili.
- Le operazioni batch aggiornano lo stato delle app direttamente nella lista.
- L'output dei comandi viene normalizzato per rimuovere righe di progresso ridondanti.
