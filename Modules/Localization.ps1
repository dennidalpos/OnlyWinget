
function Get-LocalizedStrings {
    
    param(
        [string]$Culture = "it-IT"
    )

    switch ($Culture) {
        "en-US" {
            return @{
                Add = 'Add'
                Edit = 'Edit'
                Remove = 'Remove'
                Search = 'Search App'
                Apply = 'Install/Upgrade'
                Save = 'Save'

                Title = 'Application Manager'
                Name = 'Name'
                Id = 'Winget ID'
                Action = 'Action'
                Status = 'Status'

                Install = 'Install'
                Uninstall = 'Uninstall'
                Pause = 'Pause'

                InputNameTitle = 'Add App'
                InputNamePrompt = 'Enter app name:'
                InputIdTitle = 'Winget ID'
                InputIdPrompt = 'Enter Winget ID (e.g. Microsoft.VisualStudioCode):'

                InvalidIdTitle = 'Invalid ID'
                InvalidIdText = "ID '{0}' not found in Winget store. Try again."
                DuplicateIdTitle = 'Duplicate ID'
                DuplicateIdText = "ID '{0}' is already in the list."

                SearchTitle = 'Search App'
                SearchPrompt = 'Search term:'

                SaveSuccessTitle = 'Saved'
                SaveSuccessText = 'List saved successfully.'

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

                RunningText = 'Operation in progress...'

                WingetNotFoundTitle = 'Winget not found'
                WingetNotFoundText = 'winget is not available. Install/update "App Installer" from Microsoft Store.'

                CloseButton = 'Close'
                UseIdButton = 'Use this ID'
                VersionHeader = 'Version'
            }
        }
        default {
            return @{
                Add = 'Aggiungi'
                Edit = 'Modifica'
                Remove = 'Rimuovi'
                Search = 'Cerca App'
                Apply = 'Installa/Aggiorna'
                Save = 'Salva'

                Title = 'Gestione Applicazioni'
                Name = 'Nome'
                Id = 'ID Winget'
                Action = 'Azione'
                Status = 'Stato'

                Install = 'Installa'
                Uninstall = 'Disinstalla'
                Pause = 'Pausa'

                InputNameTitle = 'Aggiungi App'
                InputNamePrompt = 'Inserisci il nome dell''app:'
                InputIdTitle = 'ID Winget'
                InputIdPrompt = 'Inserisci ID Winget (es. Microsoft.VisualStudioCode):'

                InvalidIdTitle = 'Errore ID'
                InvalidIdText = "ID '{0}' non trovato nello store Winget. Riprova."
                DuplicateIdTitle = 'ID duplicato'
                DuplicateIdText = "L'ID '{0}' e' gia' presente in lista."

                SearchTitle = 'Cerca App'
                SearchPrompt = 'Termine di ricerca:'

                SaveSuccessTitle = 'Salvato'
                SaveSuccessText = 'Lista salvata correttamente.'

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

                RunningText = 'Operazione in corso...'

                WingetNotFoundTitle = 'Winget non trovato'
                WingetNotFoundText = 'winget non risulta disponibile. Installa/aggiorna "App Installer" dal Microsoft Store.'

                CloseButton = 'Chiudi'
                UseIdButton = 'Usa questo ID'
                VersionHeader = 'Versione'
            }
        }
    }
}

Export-ModuleMember -Function Get-LocalizedStrings
