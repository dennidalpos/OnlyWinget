using System.Globalization;

namespace OnlyWinget;

public static class TextResources
{
    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Nav_Presets"] = "Presets",
        ["Nav_Search"] = "Search",
        ["Nav_Updates"] = "Updates",
        ["Nav_Activity"] = "Activity",
        ["Presets_Title"] = "Presets",
        ["Preset_Name"] = "Preset name",
        ["Package_Id"] = "Package id",
        ["Package_Source"] = "Source",
        ["Import_Json"] = "Import JSON",
        ["Export_Json"] = "Export JSON",
        ["Search_Query"] = "Search winget packages",
        ["Updates_Title"] = "Updates",
        ["Activity_Title"] = "Activity",
        ["Empty_Presets"] = "Create or import a preset to start.",
        ["Empty_Packages"] = "No packages in the active preset.",
        ["Empty_Search"] = "Search results will appear here.",
        ["Empty_Updates"] = "Refresh updates to review available upgrades.",
        ["Empty_Activity"] = "No activity yet.",
        ["Command_Preset_Add"] = "Add preset",
        ["Command_Preset_Rename"] = "Rename",
        ["Command_Preset_Remove"] = "Remove preset",
        ["Command_PresetPackage_Add"] = "Add package",
        ["Command_PresetPackage_Edit"] = "Edit package",
        ["Command_PresetPackage_Remove"] = "Remove selected",
        ["Command_Preset_Import"] = "Import",
        ["Command_Preset_Export"] = "Export",
        ["Command_Workspace_Save"] = "Save workspace",
        ["Command_Preset_ApplyInstall"] = "Install preset",
        ["Command_Preset_ApplyUninstall"] = "Uninstall preset",
        ["Command_Search_Execute"] = "Search",
        ["Command_Search_AddSelected"] = "Add selected",
        ["Command_Updates_Refresh"] = "Refresh",
        ["Command_Updates_ApplySelected"] = "Apply selected",
        ["Command_Operation_Cancel"] = "Cancel",
        ["Command_Select_All"] = "Select all",
        ["Command_Activity_Clear"] = "Clear"
    };

    private static readonly IReadOnlyDictionary<string, string> Italian = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Nav_Presets"] = "Preset",
        ["Nav_Search"] = "Cerca",
        ["Nav_Updates"] = "Aggiornamenti",
        ["Nav_Activity"] = "Attivita",
        ["Presets_Title"] = "Preset",
        ["Preset_Name"] = "Nome preset",
        ["Package_Id"] = "ID pacchetto",
        ["Package_Source"] = "Origine",
        ["Import_Json"] = "JSON importazione",
        ["Export_Json"] = "JSON esportazione",
        ["Search_Query"] = "Cerca pacchetti winget",
        ["Updates_Title"] = "Aggiornamenti",
        ["Activity_Title"] = "Attivita",
        ["Empty_Presets"] = "Crea o importa un preset per iniziare.",
        ["Empty_Packages"] = "Nessun pacchetto nel preset attivo.",
        ["Empty_Search"] = "I risultati della ricerca appariranno qui.",
        ["Empty_Updates"] = "Aggiorna l'elenco per vedere gli upgrade disponibili.",
        ["Empty_Activity"] = "Nessuna attivita.",
        ["Command_Preset_Add"] = "Aggiungi preset",
        ["Command_Preset_Rename"] = "Rinomina",
        ["Command_Preset_Remove"] = "Rimuovi preset",
        ["Command_PresetPackage_Add"] = "Aggiungi pacchetto",
        ["Command_PresetPackage_Edit"] = "Modifica pacchetto",
        ["Command_PresetPackage_Remove"] = "Rimuovi selezionati",
        ["Command_Preset_Import"] = "Importa",
        ["Command_Preset_Export"] = "Esporta",
        ["Command_Workspace_Save"] = "Salva workspace",
        ["Command_Preset_ApplyInstall"] = "Installa preset",
        ["Command_Preset_ApplyUninstall"] = "Disinstalla preset",
        ["Command_Search_Execute"] = "Cerca",
        ["Command_Search_AddSelected"] = "Aggiungi selezionati",
        ["Command_Updates_Refresh"] = "Aggiorna",
        ["Command_Updates_ApplySelected"] = "Applica selezionati",
        ["Command_Operation_Cancel"] = "Annulla",
        ["Command_Select_All"] = "Seleziona tutto",
        ["Command_Activity_Clear"] = "Svuota"
    };

    public static string Get(string key)
    {
        var resources = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("it", StringComparison.OrdinalIgnoreCase)
            ? Italian
            : English;
        return resources.TryGetValue(key, out var value) ? value : key;
    }
}
