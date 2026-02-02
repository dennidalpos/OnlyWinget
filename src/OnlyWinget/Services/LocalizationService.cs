using System.Collections.Generic;
using System.Globalization;

namespace OnlyWinget.Services;

public sealed class LocalizationService
{
    public LocalizedStrings GetStrings(string? culture = null)
    {
        var selected = culture ?? CultureInfo.CurrentUICulture.Name;
        return selected switch
        {
            "en-US" => LocalizedStrings.English,
            _ => LocalizedStrings.Italian
        };
    }
}

public sealed class LocalizedStrings
{
    public string Add { get; init; } = string.Empty;
    public string Edit { get; init; } = string.Empty;
    public string Remove { get; init; } = string.Empty;
    public string Search { get; init; } = string.Empty;
    public string Apply { get; init; } = string.Empty;
    public string Save { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Id { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Install { get; init; } = string.Empty;
    public string Uninstall { get; init; } = string.Empty;
    public string Pause { get; init; } = string.Empty;
    public string InputNameTitle { get; init; } = string.Empty;
    public string InputNamePrompt { get; init; } = string.Empty;
    public string InputIdTitle { get; init; } = string.Empty;
    public string InputIdPrompt { get; init; } = string.Empty;
    public string InvalidIdTitle { get; init; } = string.Empty;
    public string InvalidIdText { get; init; } = string.Empty;
    public string DuplicateIdTitle { get; init; } = string.Empty;
    public string DuplicateIdText { get; init; } = string.Empty;
    public string SearchTitle { get; init; } = string.Empty;
    public string SearchPrompt { get; init; } = string.Empty;
    public string UpdatesTitle { get; init; } = string.Empty;
    public string Updates { get; init; } = string.Empty;
    public string RefreshUpdates { get; init; } = string.Empty;
    public string ApplyUpdates { get; init; } = string.Empty;
    public string SaveSuccessTitle { get; init; } = string.Empty;
    public string SaveSuccessText { get; init; } = string.Empty;
    public string Tab { get; init; } = string.Empty;
    public string NewTab { get; init; } = string.Empty;
    public string RenameTab { get; init; } = string.Empty;
    public string DeleteTab { get; init; } = string.Empty;
    public string TabNameTitle { get; init; } = string.Empty;
    public string TabNamePrompt { get; init; } = string.Empty;
    public string TabRenameTitle { get; init; } = string.Empty;
    public string TabRenamePrompt { get; init; } = string.Empty;
    public string TabExistsTitle { get; init; } = string.Empty;
    public string TabExistsText { get; init; } = string.Empty;
    public string NoTabToDeleteTitle { get; init; } = string.Empty;
    public string NoTabToDeleteText { get; init; } = string.Empty;
    public string RunningText { get; init; } = string.Empty;
    public string WingetNotFoundTitle { get; init; } = string.Empty;
    public string WingetNotFoundText { get; init; } = string.Empty;
    public string CloseButton { get; init; } = string.Empty;
    public string UseIdButton { get; init; } = string.Empty;
    public string VersionHeader { get; init; } = string.Empty;
    public string AvailableHeader { get; init; } = string.Empty;

    public static LocalizedStrings English => new()
    {
        Add = "Add",
        Edit = "Edit",
        Remove = "Remove",
        Search = "Search App",
        Apply = "Install/Upgrade",
        Save = "Save",
        Title = "Application Manager",
        Name = "Name",
        Id = "Winget ID",
        Action = "Action",
        Status = "Status",
        Install = "Install",
        Uninstall = "Uninstall",
        Pause = "Pause",
        InputNameTitle = "Add App",
        InputNamePrompt = "Enter app name:",
        InputIdTitle = "Winget ID",
        InputIdPrompt = "Enter Winget ID (e.g. Microsoft.VisualStudioCode):",
        InvalidIdTitle = "Invalid ID",
        InvalidIdText = "ID '{0}' not found in Winget store. Try again.",
        DuplicateIdTitle = "Duplicate ID",
        DuplicateIdText = "ID '{0}' is already in the list.",
        SearchTitle = "Search App",
        SearchPrompt = "Search term:",
        UpdatesTitle = "Available updates",
        Updates = "Updates",
        RefreshUpdates = "Refresh list",
        ApplyUpdates = "Apply updates",
        SaveSuccessTitle = "Saved",
        SaveSuccessText = "List saved successfully.",
        Tab = "Tab",
        NewTab = "New",
        RenameTab = "Rename",
        DeleteTab = "Delete",
        TabNameTitle = "New Tab",
        TabNamePrompt = "New tab name:",
        TabRenameTitle = "Rename Tab",
        TabRenamePrompt = "New name for the tab:",
        TabExistsTitle = "Tab name exists",
        TabExistsText = "A tab with this name already exists.",
        NoTabToDeleteTitle = "Delete Tab",
        NoTabToDeleteText = "You cannot delete the only existing tab.",
        RunningText = "Operation in progress...",
        WingetNotFoundTitle = "Winget not found",
        WingetNotFoundText = "winget is not available. Install/update \"App Installer\" from Microsoft Store.",
        CloseButton = "Close",
        UseIdButton = "Use this ID",
        VersionHeader = "Version",
        AvailableHeader = "Available"
    };

    public static LocalizedStrings Italian => new()
    {
        Add = "Aggiungi",
        Edit = "Modifica",
        Remove = "Rimuovi",
        Search = "Cerca App",
        Apply = "Installa/Aggiorna",
        Save = "Salva",
        Title = "Gestione Applicazioni",
        Name = "Nome",
        Id = "ID Winget",
        Action = "Azione",
        Status = "Stato",
        Install = "Installa",
        Uninstall = "Disinstalla",
        Pause = "Pausa",
        InputNameTitle = "Aggiungi App",
        InputNamePrompt = "Inserisci il nome dell'app:",
        InputIdTitle = "ID Winget",
        InputIdPrompt = "Inserisci ID Winget (es. Microsoft.VisualStudioCode):",
        InvalidIdTitle = "Errore ID",
        InvalidIdText = "ID '{0}' non trovato nello store Winget. Riprova.",
        DuplicateIdTitle = "ID duplicato",
        DuplicateIdText = "L'ID '{0}' è già presente in lista.",
        SearchTitle = "Cerca App",
        SearchPrompt = "Termine di ricerca:",
        UpdatesTitle = "Aggiornamenti disponibili",
        Updates = "Aggiornamenti",
        RefreshUpdates = "Aggiorna elenco",
        ApplyUpdates = "Applica aggiornamenti",
        SaveSuccessTitle = "Salvato",
        SaveSuccessText = "Lista salvata correttamente.",
        Tab = "Scheda",
        NewTab = "Nuova",
        RenameTab = "Rinomina",
        DeleteTab = "Rimuovi",
        TabNameTitle = "Nuova scheda",
        TabNamePrompt = "Nome della nuova scheda:",
        TabRenameTitle = "Rinomina scheda",
        TabRenamePrompt = "Nuovo nome per la scheda:",
        TabExistsTitle = "Nome scheda esistente",
        TabExistsText = "Esiste già una scheda con questo nome.",
        NoTabToDeleteTitle = "Eliminazione scheda",
        NoTabToDeleteText = "Non puoi eliminare l'unica scheda esistente.",
        RunningText = "Operazione in corso...",
        WingetNotFoundTitle = "Winget non trovato",
        WingetNotFoundText = "winget non risulta disponibile. Installa/aggiorna \"App Installer\" dal Microsoft Store.",
        CloseButton = "Chiudi",
        UseIdButton = "Usa questo ID",
        VersionHeader = "Versione",
        AvailableHeader = "Disponibile"
    };
}
