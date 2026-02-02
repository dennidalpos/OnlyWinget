# Localization.ps1
# Modulo per la gestione delle stringhe localizzate dell'interfaccia utente

function Get-LocalizedStrings {
    <#
    .SYNOPSIS
        Restituisce un hashtable con tutte le stringhe localizzate.
    .DESCRIPTION
        Centralizza tutte le stringhe dell'interfaccia utente per facilitare
        la localizzazione e la manutenzione.
    .PARAMETER Culture
        Codice cultura (es. "it-IT", "en-US"). Default: italiano.
    .OUTPUTS
        Hashtable con le stringhe localizzate.
    #>
    param(
        [string]$Culture = "it-IT"
    )

    switch ($Culture) {
        "en-US" {
            return @{
                # Pulsanti principali
                Add = 'Add'
                Edit = 'Edit'
                Remove = 'Remove'
                Search = 'Search App'
                Apply = 'Install/Upgrade'
                Save = 'Save'

                # Titoli e intestazioni
                Title = 'Application Manager'
                Name = 'Name'
                Id = 'Winget ID'
                Action = 'Action'
                Status = 'Status'

                # Azioni
                Install = 'Install'
                Uninstall = 'Uninstall'
                Pause = 'Pause'

                # Dialog aggiungi app
                InputNameTitle = 'Add App'
                InputNamePrompt = 'Enter app name:'
                InputIdTitle = 'Winget ID'
                InputIdPrompt = 'Enter Winget ID (e.g. Microsoft.VisualStudioCode):'

                # Messaggi errore
                InvalidIdTitle = 'Invalid ID'
                InvalidIdText = "ID '{0}' not found in Winget store. Try again."
                DuplicateIdTitle = 'Duplicate ID'
                DuplicateIdText = "ID '{0}' is already in the list."

                # Ricerca
                SearchTitle = 'Search App'
                SearchPrompt = 'Search term:'

                # Salvataggio
                SaveSuccessTitle = 'Saved'
                SaveSuccessText = 'List saved successfully.'

                # Schede
                Tab = 'Tab'
                NewTab = 'New'
                RenameTab = 'Rename'
                DeleteTab = 'Delete'
                TabNameTitle = 'New Tab'
                TabNamePrompt = 'New tab name:'
                TabRenameTitle = 'Rename Tab'
                TabRenamePrompt = 'New name for the tab:'
                TabExistsTitle = 'Tab name exists'
                TabExistsText = 'A tab with this name already exists.'
                NoTabToDeleteTitle = 'Delete Tab'
                NoTabToDeleteText = 'You cannot delete the only existing tab.'

                # Stati
                RunningText = 'Operation in progress...'

                # Winget
                WingetNotFoundTitle = 'Winget not found'
                WingetNotFoundText = 'winget is not available. Install/update "App Installer" from Microsoft Store.'

                # UI elementi
                CloseButton = 'Close'
                UseIdButton = 'Use this ID'
                VersionHeader = 'Version'
            }
        }
        default {
            # Italiano (default)
            return @{
                # Pulsanti principali
                Add = 'Aggiungi'
                Edit = 'Modifica'
                Remove = 'Rimuovi'
                Search = 'Cerca App'
                Apply = 'Installa/Aggiorna'
                Save = 'Salva'

                # Titoli e intestazioni
                Title = 'Gestione Applicazioni'
                Name = 'Nome'
                Id = 'ID Winget'
                Action = 'Azione'
                Status = 'Stato'

                # Azioni
                Install = 'Installa'
                Uninstall = 'Disinstalla'
                Pause = 'Pausa'

                # Dialog aggiungi app
                InputNameTitle = 'Aggiungi App'
                InputNamePrompt = 'Inserisci il nome dell''app:'
                InputIdTitle = 'ID Winget'
                InputIdPrompt = 'Inserisci ID Winget (es. Microsoft.VisualStudioCode):'

                # Messaggi errore
                InvalidIdTitle = 'Errore ID'
                InvalidIdText = "ID '{0}' non trovato nello store Winget. Riprova."
                DuplicateIdTitle = 'ID duplicato'
                DuplicateIdText = "L'ID '{0}' e' gia' presente in lista."

                # Ricerca
                SearchTitle = 'Cerca App'
                SearchPrompt = 'Termine di ricerca:'

                # Salvataggio
                SaveSuccessTitle = 'Salvato'
                SaveSuccessText = 'Lista salvata correttamente.'

                # Schede
                Tab = 'Scheda'
                NewTab = 'Nuova'
                RenameTab = 'Rinomina'
                DeleteTab = 'Rimuovi'
                TabNameTitle = 'Nuova scheda'
                TabNamePrompt = 'Nome della nuova scheda:'
                TabRenameTitle = 'Rinomina scheda'
                TabRenamePrompt = 'Nuovo nome per la scheda:'
                TabExistsTitle = 'Nome scheda esistente'
                TabExistsText = 'Esiste gia'' una scheda con questo nome.'
                NoTabToDeleteTitle = 'Eliminazione scheda'
                NoTabToDeleteText = 'Non puoi eliminare l''unica scheda esistente.'

                # Stati
                RunningText = 'Operazione in corso...'

                # Winget
                WingetNotFoundTitle = 'Winget non trovato'
                WingetNotFoundText = 'winget non risulta disponibile. Installa/aggiorna "App Installer" dal Microsoft Store.'

                # UI elementi
                CloseButton = 'Chiudi'
                UseIdButton = 'Usa questo ID'
                VersionHeader = 'Versione'
            }
        }
    }
}

# Esporta la funzione
Export-ModuleMember -Function Get-LocalizedStrings
