// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace OnlyWinget.Services;

public sealed class WingetOutputClassifier
{
    private static readonly int[] NoUpgradeNeededCodes =
    {
        -1978335189,
        -1978335135,
        -1978334963,
        -1978334962
    };

    private static readonly int[] AlreadyInstalledCodes =
    {
        -1978335135,
        -1978334963,
        -1978334962
    };

    public bool IsNoUpgradeNeeded(int exitCode) => NoUpgradeNeededCodes.Contains(exitCode);

    public bool IsAlreadyInstalled(int exitCode) => AlreadyInstalledCodes.Contains(exitCode);

    public bool IsNoApplicableUpgrade(WingetCommandResult result)
    {
        var output = NormalizeWingetOutput(result.Output);
        return ContainsAny(output,
            "no applicable upgrade found",
            "does not apply to your system or requirements",
            "no applicable update found",
            "nessun aggiornamento applicabile",
            "non si applica al sistema",
            "non applicabile ai requisiti");
    }

    public bool IsAlreadyInstalled(WingetCommandResult result)
    {
        if (IsAlreadyInstalled(result.ExitCode))
        {
            return true;
        }

        var output = NormalizeWingetOutput(result.Output);
        return ContainsAny(output,
            "already installed",
            "another version of this application is already installed",
            "a newer version of this package is already installed",
            "already exists",
            "gia installata",
            "gia' installata",
            "versione superiore presente",
            "altra versione installata");
    }

    public bool ShouldFallbackToInstall(WingetCommandResult result)
    {
        if (result.ExitCode == 0 || IsNoUpgradeNeeded(result.ExitCode))
        {
            return false;
        }

        if (result.ExitCode == -1978335212)
        {
            return true;
        }

        var output = NormalizeWingetOutput(result.Output);
        return ContainsAny(output,
            "no installed package found matching input criteria",
            "no package found installed",
            "package is not installed",
            "nessun pacchetto installato trovato",
            "pacchetto non installato");
    }

    public IReadOnlyList<string> GetRelevantOutputLines(string output)
    {
        var normalized = NormalizeWingetOutput(output);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Array.Empty<string>();
        }

        return normalized
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => !line.All(c => c is '-' or '/' or '\\' or '|'))
            .Where(IsRelevantOutputLine)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool TryGetProgressPercentage(string line, out int percentage)
    {
        percentage = 0;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var match = Regex.Match(NormalizeWingetOutput(line), @"(?<!\d)(\d{1,3})%");
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var parsed))
        {
            return false;
        }

        percentage = Math.Max(0, Math.Min(100, parsed));
        return true;
    }

    public bool ShouldLogOutputLine(string line)
    {
        return !string.IsNullOrWhiteSpace(line) && IsRelevantOutputLine(line);
    }

    public string GetErrorMessage(int exitCode, string? localeCode = null)
    {
        return UseEnglish(localeCode)
            ? GetEnglishErrorMessage(exitCode)
            : GetItalianErrorMessage(exitCode);
    }

    public string GetResolutionHint(int exitCode, string? localeCode = null)
    {
        return UseEnglish(localeCode)
            ? GetEnglishResolutionHint(exitCode)
            : GetItalianResolutionHint(exitCode);
    }

    private static string GetItalianResolutionHint(int exitCode)
    {
        return exitCode switch
        {
            0 => string.Empty,
            -1978335207 => "Rieseguire OnlyWinget come Amministratore.",
            -2147009240 => "Rieseguire OnlyWinget come Amministratore.",
            -1978335146 => "Rieseguire OnlyWinget senza privilegi di Amministratore.",
            -1978334963 => "È presente un'altra versione. Rimuoverla manualmente, poi reinstallare tramite OnlyWinget.",
            -1978334962 => "La versione corrente è già superiore. Nessuna azione necessaria.",
            -1978335153 => "La versione disponibile non è più recente di quella installata.",
            -1978334956 => "Aggiornamento automatico non supportato. Rimuovere manualmente il software, poi reinstallare tramite OnlyWinget.",
            -1978335184 => "Disinstallare manualmente il software esistente, poi reinstallare tramite OnlyWinget.",
            -1978334975 => "Chiudere l'applicazione e riprovare.",
            -1978334973 => "Chiudere le applicazioni che usano i file coinvolti e riprovare.",
            -1978335224 => "Verificare la connessione di rete e riprovare.",
            -1978334971 => "Liberare spazio su disco e riprovare.",
            -1978334970 => "Chiudere altre applicazioni per liberare memoria e riprovare.",
            -1978334969 => "Verificare la connessione di rete e riprovare.",
            -1978334967 => "Riavviare il sistema per completare l'operazione.",
            -1978334966 => "Riavviare il sistema, poi riprovare.",
            -1978334965 => "Riavvio in corso. Riprovare dopo il riavvio.",
            -1978334964 => "Riprovare l'operazione.",
            -1978335174 => "Contattare l'amministratore IT.",
            -1978334961 => "Contattare l'amministratore IT.",
            -1978335215 => "Riprovare. Se l'errore persiste, segnalare al manutentore del pacchetto.",
            -2145844844 => "Consultare il log. Potrebbe richiedere installazione manuale.",
            -1978334955 => "Consultare il log. Potrebbe richiedere installazione manuale.",
            -1978335212 => "Verificare l'ID del pacchetto e la sorgente configurata. Se il pacchetto appare nella lista aggiornamenti, OnlyWinget riprovera usando il nome installato.",
            -2147009295 => "Il pacchetto MSIX non risulta installato per l'utente corrente.",
            -1978335231 => "Riprovare. Se l'errore persiste, aggiornare winget.",
            -1978335189 => "Già alla versione più recente.",
            -1978335135 => "Già installata. Nessuna azione necessaria.",
            -1978334972 => "Installare le dipendenze richieste e riprovare.",
            -1978334960 => "Una o più dipendenze hanno fallito. Consultare il log.",
            -1978335216 => "Nessun installer compatibile. Verificare architettura e scope.",
            -1978335128 => "Pacchetto bloccato da pin winget. Rimuovere il pin per procedere.",
            -1978335230 => "Verificare la configurazione del pacchetto in OnlyWinget.",
            9999 => "Verificare che winget sia disponibile e riprovare.",
            _ => "Consultare il log per i dettagli."
        };
    }

    private static string GetEnglishResolutionHint(int exitCode)
    {
        return exitCode switch
        {
            0 => string.Empty,
            -1978335207 => "Re-run OnlyWinget as Administrator.",
            -2147009240 => "Re-run OnlyWinget as Administrator.",
            -1978335146 => "Re-run OnlyWinget without Administrator privileges.",
            -1978334963 => "Another version is installed. Remove it manually, then reinstall via OnlyWinget.",
            -1978334962 => "Current version is already newer. No action required.",
            -1978335153 => "The available version is not newer than the installed one.",
            -1978334956 => "Automatic upgrade not supported. Remove the software manually, then reinstall via OnlyWinget.",
            -1978335184 => "Manually uninstall the existing software, then reinstall via OnlyWinget.",
            -1978334975 => "Close the application and retry.",
            -1978334973 => "Close applications using the involved files and retry.",
            -1978335224 => "Check network connection and retry.",
            -1978334971 => "Free up disk space and retry.",
            -1978334970 => "Close other applications to free memory and retry.",
            -1978334969 => "Check network connection and retry.",
            -1978334967 => "Restart the system to complete the operation.",
            -1978334966 => "Restart the system, then retry.",
            -1978334965 => "Restart in progress. Retry after restarting.",
            -1978334964 => "Retry the operation.",
            -1978335174 => "Contact your IT administrator.",
            -1978334961 => "Contact your IT administrator.",
            -1978335215 => "Retry. If the issue persists, report to the package maintainer.",
            -2145844844 => "Check the log. Manual installation may be required.",
            -1978334955 => "Check the log. Manual installation may be required.",
            -1978335212 => "Verify the package ID and configured source. If the package appears in the updates list, OnlyWinget will retry using the installed package name.",
            -2147009295 => "The MSIX package is not installed for the current user.",
            -1978335231 => "Retry. If the issue persists, update winget.",
            -1978335189 => "Already at the latest version.",
            -1978335135 => "Already installed. No action required.",
            -1978334972 => "Install required dependencies and retry.",
            -1978334960 => "One or more dependencies failed. Check the log.",
            -1978335216 => "No compatible installer found. Check architecture and scope.",
            -1978335128 => "Package is blocked by a winget pin. Remove the pin to proceed.",
            -1978335230 => "Check the package configuration in OnlyWinget.",
            9999 => "Check that winget is available and retry.",
            _ => "Check the log for details."
        };
    }

    private static string GetItalianErrorMessage(int exitCode)
    {
        return exitCode switch
        {
            0 => "OK",
            -1978335231 => "Errore interno",
            -1978335230 => "Argomenti non validi",
            -1978335229 => "Comando fallito",
            -1978335228 => "Apertura manifest fallita",
            -1978335227 => "Annullato",
            -1978335226 => "ShellExecute fallito",
            -1978335225 => "Versione manifest non supportata",
            -1978335224 => "Download fallito",
            -1978335222 => "Indice corrotto",
            -1978335221 => "Origini non valide",
            -1978335220 => "Nome origine già esistente",
            -1978335219 => "Tipo origine non valido",
            -1978335217 => "Dati origine mancanti",
            -1978335216 => "Nessun installer applicabile",
            -1978335215 => "Hash non corrisponde",
            -1978335214 => "Nome origine non esiste",
            -1978335212 => "App non trovata",
            -2147009295 => "Pacchetto MSIX non trovato",
            -1978335211 => "Nessuna origine configurata",
            -1978335210 => "Più app trovate",
            -1978335209 => "Manifest non trovato",
            -1978335207 => "Richiesti privilegi admin",
            -2147009240 => "Richiesti privilegi admin",
            -1978335205 => "MS Store bloccato da policy",
            -1978335204 => "App MS Store bloccata da policy",
            -1978335203 => "Funzione sperimentale disabilitata",
            -1978335202 => "Installazione MS Store fallita",
            -1978335191 => "Validazione manifest fallita",
            -1978335190 => "Manifest non valido",
            -1978335189 => "Nessun aggiornamento",
            -1978335188 => "Upgrade --all con errori",
            -1978335187 => "Controllo sicurezza fallito",
            -1978335186 => "Dimensione download errata",
            -1978335185 => "Info disinstallazione mancanti",
            -1978335184 => "Disinstallazione fallita",
            -1978335180 => "Import installazione fallito",
            -1978335179 => "Non tutti i pacchetti trovati",
            -1978335174 => "Bloccato da policy",
            -1978335173 => "Errore REST API",
            -1978335163 => "Apertura origine fallita",
            -1978335157 => "Apertura origini fallita",
            -1978335153 => "Versione upgrade non più recente",
            -1978335150 => "Installazione portable fallita",
            -1978335147 => "Portable già esistente",
            -1978335146 => "Installer proibisce elevazione",
            -1978335145 => "Disinstallazione portable fallita",
            -1978335141 => "Nested installer non trovato",
            -1978335140 => "Estrazione archivio fallita",
            -1978335137 => "Percorso installazione richiesto",
            -1978335136 => "Scansione malware fallita",
            -1978335135 => "Già installata",
            -1978335131 => "Una o più installazioni fallite",
            -1978335130 => "Una o più disinstallazioni fallite",
            -1978335128 => "Bloccato da pin",
            -1978335127 => "Pacchetto stub",
            -1978335125 => "Download dipendenze fallito",
            -1978335123 => "Servizio non disponibile",
            -1978335115 => "Autenticazione fallita",
            -1978335111 => "Info riparazione mancanti",
            -1978335109 => "Riparazione fallita",
            -1978335108 => "Riparazione non supportata",
            -1978335098 => "Installer zero byte",
            -1978334975 => "App in uso",
            -1978334974 => "Installazione in corso",
            -1978334973 => "File in uso",
            -1978334972 => "Dipendenza mancante",
            -1978334971 => "Disco pieno",
            -1978334970 => "Memoria insufficiente",
            -1978334969 => "Rete richiesta",
            -1978334968 => "Contattare supporto",
            -1978334967 => "Riavvio per completare",
            -1978334966 => "Riavvio per installare",
            -1978334965 => "Riavvio avviato",
            -1978334964 => "Annullato dall'utente",
            -1978334963 => "Altra versione installata",
            -1978334962 => "Versione superiore presente",
            -1978334961 => "Bloccato da policy",
            -1978334960 => "Dipendenze fallite",
            -1978334959 => "App usata da altra applicazione",
            -1978334958 => "Parametro non valido",
            -1978334957 => "Sistema non supportato",
            -1978334956 => "Upgrade non supportato",
            -1978334955 => "Errore installer personalizzato",
            -2145844844 => "Errore installer",
            -2147023673 => "Operazione annullata dall'utente",
            9999 => "Errore esecuzione",
            _ => $"Errore ({exitCode})"
        };
    }

    private static string GetEnglishErrorMessage(int exitCode)
    {
        return exitCode switch
        {
            0 => "OK",
            -1978335231 => "Internal error",
            -1978335230 => "Invalid arguments",
            -1978335229 => "Command failed",
            -1978335228 => "Manifest open failed",
            -1978335227 => "Cancelled",
            -1978335226 => "ShellExecute failed",
            -1978335225 => "Unsupported manifest version",
            -1978335224 => "Download failed",
            -1978335222 => "Corrupted index",
            -1978335221 => "Invalid sources",
            -1978335220 => "Source name already exists",
            -1978335219 => "Invalid source type",
            -1978335217 => "Missing source data",
            -1978335216 => "No applicable installer",
            -1978335215 => "Hash mismatch",
            -1978335214 => "Source name does not exist",
            -1978335212 => "App not found",
            -2147009295 => "MSIX package not found",
            -1978335211 => "No source configured",
            -1978335210 => "Multiple apps found",
            -1978335209 => "Manifest not found",
            -1978335207 => "Administrator privileges required",
            -2147009240 => "Administrator privileges required",
            -1978335205 => "Microsoft Store blocked by policy",
            -1978335204 => "Microsoft Store app blocked by policy",
            -1978335203 => "Experimental feature disabled",
            -1978335202 => "Microsoft Store install failed",
            -1978335191 => "Manifest validation failed",
            -1978335190 => "Invalid manifest",
            -1978335189 => "No update available",
            -1978335188 => "Upgrade --all completed with errors",
            -1978335187 => "Security check failed",
            -1978335186 => "Invalid download size",
            -1978335185 => "Missing uninstall information",
            -1978335184 => "Uninstall failed",
            -1978335180 => "Import install failed",
            -1978335179 => "Not all packages found",
            -1978335174 => "Blocked by policy",
            -1978335173 => "REST API error",
            -1978335163 => "Source open failed",
            -1978335157 => "Sources open failed",
            -1978335153 => "Upgrade version is not newer",
            -1978335150 => "Portable install failed",
            -1978335147 => "Portable already exists",
            -1978335146 => "Installer forbids elevation",
            -1978335145 => "Portable uninstall failed",
            -1978335141 => "Nested installer not found",
            -1978335140 => "Archive extraction failed",
            -1978335137 => "Install path required",
            -1978335136 => "Malware scan failed",
            -1978335135 => "Already installed",
            -1978335131 => "One or more installs failed",
            -1978335130 => "One or more uninstalls failed",
            -1978335128 => "Blocked by pin",
            -1978335127 => "Stub package",
            -1978335125 => "Dependency download failed",
            -1978335123 => "Service unavailable",
            -1978335115 => "Authentication failed",
            -1978335111 => "Missing repair information",
            -1978335109 => "Repair failed",
            -1978335108 => "Repair not supported",
            -1978335098 => "Installer is empty",
            -1978334975 => "App in use",
            -1978334974 => "Install in progress",
            -1978334973 => "File in use",
            -1978334972 => "Missing dependency",
            -1978334971 => "Disk full",
            -1978334970 => "Insufficient memory",
            -1978334969 => "Network required",
            -1978334968 => "Contact support",
            -1978334967 => "Restart required to complete",
            -1978334966 => "Restart required to install",
            -1978334965 => "Restart initiated",
            -1978334964 => "Cancelled by user",
            -1978334963 => "Another version installed",
            -1978334962 => "Newer version already present",
            -1978334961 => "Blocked by policy",
            -1978334960 => "Dependencies failed",
            -1978334959 => "App used by another application",
            -1978334958 => "Invalid parameter",
            -1978334957 => "Unsupported system",
            -1978334956 => "Upgrade not supported",
            -1978334955 => "Custom installer error",
            -2145844844 => "Installer error",
            -2147023673 => "Operation cancelled by user",
            9999 => "Execution error",
            _ => $"Error ({exitCode})"
        };
    }

    private static string NormalizeWingetOutput(string output)
    {
        if (string.IsNullOrEmpty(output))
        {
            return string.Empty;
        }

        var noAnsi = Regex.Replace(output, @"\x1B\[[0-9;?]*[ -/]*[@-~]", string.Empty);
        return noAnsi.Replace('\b', ' ');
    }

    private static bool IsRelevantOutputLine(string line)
    {
        if (Regex.IsMatch(line, @"\b\d{1,3}%\b"))
        {
            return true;
        }

        return ContainsAny(line,
            "error",
            "failed",
            "failure",
            "not found",
            "applicable upgrade",
            "applicable update",
            "requirements",
            "already installed",
            "newer version",
            "newer package version",
            "download",
            "installing",
            "installed",
            "upgrading",
            "upgraded",
            "uninstalling",
            "uninstalled",
            "cancelled",
            "requires admin",
            "errore",
            "non trovato",
            "aggiornamento applicabile",
            "requisiti",
            "gia installata",
            "gia' installata",
            "versione superiore",
            "download",
            "installazione",
            "installata",
            "aggiornamento",
            "aggiornata",
            "disinstallazione",
            "disinstallata",
            "annullata",
            "privilegi admin",
            "privilegi di amministratore",
            "amministratore necessari");
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    private static bool UseEnglish(string? localeCode)
    {
        return !string.IsNullOrWhiteSpace(localeCode)
            && localeCode.StartsWith("en", StringComparison.OrdinalIgnoreCase);
    }
}
