# OnlyWinget

Interfaccia grafica in PowerShell/WPF per gestire un elenco di applicazioni Windows tramite **winget**. Permette di salvare liste di app, organizzarle per schede e applicare in batch installazioni, aggiornamenti e disinstallazioni.

## Requisiti

- **Windows 10/11** con interfaccia grafica.
- **PowerShell** (Windows PowerShell o PowerShell 7 con supporto WPF).
- **winget** disponibile nel sistema (tramite *App Installer* dal Microsoft Store).

> Alla prima esecuzione, lo script verifica la disponibilità di `winget`. Se non è presente viene mostrato un messaggio di errore e l'app si chiude.

## Avvio rapido

1. Clona il repository o scaricalo in locale.
2. Apri PowerShell.
3. Esegui lo script principale:

```powershell
# Da cartella del progetto
./AppManager.ps1
```

Lo script si riavvierà automaticamente in modalità **STA** se necessario.

## Funzionalità principali

- **Lista applicazioni** con azione selezionabile (Install / Uninstall / Pause).
- **Ricerca winget** con selezione rapida dell'ID.
- **Batch install/upgrade/uninstall** con log dettagliato e stati per app.
- **Gestione schede** (nuova, rinomina, elimina) per organizzare gruppi di app.
- **Persistenza dati** in JSON per riaprire la lista alla prossima esecuzione.

## Struttura progetto

```
.
├── AppManager.ps1
└── Modules
    ├── DataManager.ps1
    ├── Localization.ps1
    ├── WingetUtils.ps1
    └── UI
        └── MainWindow.ps1
```

### AppManager.ps1
Script principale che:
- Avvia l'interfaccia WPF.
- Gestisce eventi UI e logica applicativa.
- Esegue i comandi winget.
- Salva/legge la lista delle app.

### Modules/
Moduli riutilizzabili:
- **DataManager.ps1**: import/export JSON, gestione schede e oggetti app.
- **Localization.ps1**: dizionario stringhe UI (IT/EN).
- **WingetUtils.ps1**: wrapper di comandi winget e parsing dei risultati.
- **UI/MainWindow.ps1**: XAML per la UI WPF.

## Formato dati (AppsList.json)

Il file viene creato accanto a `AppManager.ps1` e ha questo formato:

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
- **Action**: `Install`, `Uninstall` oppure `Pause`.

## Uso dell'interfaccia

### Aggiungere un'app
1. **Aggiungi** → inserisci nome.
2. Inserisci **ID Winget**.
3. L'app viene aggiunta con azione `Install`.

### Ricercare un'app
1. **Cerca App** → inserisci termine di ricerca.
2. Seleziona un risultato dalla lista.
3. Premi **Usa questo ID** per aggiungerla alla lista.

### Eseguire le operazioni
1. Imposta l'azione per ogni app.
2. Premi **Installa/Aggiorna**.
3. Controlla output e stato finale nella lista.

### Gestire le schede
- **Nuova**: crea una scheda.
- **Rinomina**: cambia nome scheda corrente.
- **Rimuovi**: elimina la scheda corrente (non l'ultima).

## Localizzazione

Il file `Modules/Localization.ps1` contiene stringhe in italiano e inglese. Il dizionario predefinito è **it-IT**.

## Troubleshooting

- **Winget non trovato**: installa/aggiorna *App Installer* dal Microsoft Store.
- **Errore permessi**: avvia PowerShell come amministratore se necessario per installazioni/disinstallazioni.
- **Output vuoto in ricerca**: verifica connessione Internet e la presenza del repository `winget` nelle sorgenti.

## Licenza

Specificare qui la licenza del progetto (es. MIT, Apache-2.0).
