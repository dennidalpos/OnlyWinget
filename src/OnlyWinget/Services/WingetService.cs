using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OnlyWinget.Models;

namespace OnlyWinget.Services;

public sealed class WingetService
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
        -1978334963
    };

    public bool TestAvailable()
    {
        try
        {
            var result = RunWinget("--version", Array.Empty<string>());
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public (int ExitCode, string Output) Invoke(string command, Dictionary<string, string?> parameters)
    {
        var args = new List<string> { command };
        foreach (var pair in parameters)
        {
            args.Add(pair.Key);
            if (!string.IsNullOrWhiteSpace(pair.Value))
            {
                args.Add(pair.Value);
            }
        }

        return RunWinget(null, args.ToArray());
    }

    public bool TestAppExists(string id)
    {
        var result = Invoke("show", new Dictionary<string, string?>
        {
            ["--id"] = id,
            ["--source"] = "winget",
            ["--exact"] = null,
            ["--accept-source-agreements"] = null
        });
        return result.ExitCode == 0;
    }

    public IReadOnlyList<SearchResult> Search(string query)
    {
        var result = Invoke("search", new Dictionary<string, string?>
        {
            ["--query"] = query,
            ["--source"] = "winget",
            ["--accept-source-agreements"] = null
        });

        return ParseSearchOutput(result.Output);
    }

    public IReadOnlyList<UpdateEntry> LoadUpdates()
    {
        var upgradesResult = Invoke("upgrade", new Dictionary<string, string?>
        {
            ["--source"] = "winget",
            ["--accept-source-agreements"] = null
        });

        var installedResult = Invoke("list", new Dictionary<string, string?>
        {
            ["--source"] = "winget",
            ["--accept-source-agreements"] = null
        });

        var upgrades = ParseUpgradeOutput(upgradesResult.Output);
        var installed = ParseInstalledOutput(installedResult.Output);

        var combined = new Dictionary<string, UpdateEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in installed)
        {
            combined[entry.Id] = entry;
        }

        foreach (var entry in upgrades)
        {
            combined[entry.Id] = entry;
        }

        return combined.Values
            .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public (int ExitCode, string Output) UpgradeApp(string id)
    {
        return Invoke("upgrade", new Dictionary<string, string?>
        {
            ["--id"] = id,
            ["--exact"] = null,
            ["--accept-package-agreements"] = null,
            ["--accept-source-agreements"] = null,
            ["--disable-interactivity"] = null
        });
    }

    public (int ExitCode, string Output) InstallApp(string id)
    {
        return Invoke("install", new Dictionary<string, string?>
        {
            ["--id"] = id,
            ["--exact"] = null,
            ["--accept-package-agreements"] = null,
            ["--accept-source-agreements"] = null,
            ["--disable-interactivity"] = null
        });
    }

    public (int ExitCode, string Output) UninstallApp(string id)
    {
        return Invoke("uninstall", new Dictionary<string, string?>
        {
            ["--id"] = id,
            ["--exact"] = null,
            ["--accept-source-agreements"] = null,
            ["--disable-interactivity"] = null
        });
    }

    public bool IsNoUpgradeNeeded(int exitCode) => NoUpgradeNeededCodes.Contains(exitCode);

    public bool IsAlreadyInstalled(int exitCode) => AlreadyInstalledCodes.Contains(exitCode);

    public string GetErrorMessage(int exitCode)
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
            -1978335211 => "Nessuna origine configurata",
            -1978335210 => "Più app trovate",
            -1978335209 => "Manifest non trovato",
            -1978335207 => "Richiesti privilegi admin",
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

    private static IReadOnlyList<SearchResult> ParseSearchOutput(string output)
    {
        var results = new List<SearchResult>();
        var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var headerFound = false;
        var nameStart = 0;
        var idStart = 0;
        var versionStart = 0;

        foreach (var line in lines)
        {
            if (!headerFound)
            {
                if (line.StartsWith("Nome", StringComparison.OrdinalIgnoreCase) || line.StartsWith("Name", StringComparison.OrdinalIgnoreCase))
                {
                    idStart = line.IndexOf("ID", StringComparison.Ordinal);
                    if (idStart < 0)
                    {
                        idStart = line.IndexOf("Id", StringComparison.Ordinal);
                    }

                    versionStart = line.IndexOf("Versione", StringComparison.Ordinal);
                    if (versionStart < 0)
                    {
                        versionStart = line.IndexOf("Version", StringComparison.Ordinal);
                    }

                    if (idStart >= 0 && versionStart >= 0)
                    {
                        headerFound = true;
                    }
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(line) || line.Trim().All(c => c == '-' || c == ' '))
            {
                continue;
            }

            if (line.Length < versionStart || idStart <= nameStart)
            {
                continue;
            }

            var name = line.Substring(nameStart, Math.Min(idStart - nameStart, line.Length)).Trim();
            var idEnd = versionStart > idStart ? versionStart - idStart : line.Length - idStart;
            var id = line.Substring(idStart, Math.Min(idEnd, line.Length - idStart)).Trim();
            var version = line.Length > versionStart
                ? line.Substring(versionStart).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty
                : string.Empty;

            if (!string.IsNullOrWhiteSpace(id))
            {
                results.Add(new SearchResult { Name = name, Id = id, Version = version });
            }
        }

        return results;
    }

    private static IReadOnlyList<UpdateEntry> ParseUpgradeOutput(string output)
    {
        var results = new List<UpdateEntry>();
        var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var headerFound = false;
        var nameStart = 0;
        var idStart = 0;
        var versionStart = 0;
        var availableStart = 0;

        foreach (var line in lines)
        {
            if (!headerFound)
            {
                if (line.StartsWith("Nome", StringComparison.OrdinalIgnoreCase) || line.StartsWith("Name", StringComparison.OrdinalIgnoreCase))
                {
                    idStart = line.IndexOf("ID", StringComparison.Ordinal);
                    if (idStart < 0)
                    {
                        idStart = line.IndexOf("Id", StringComparison.Ordinal);
                    }

                    versionStart = line.IndexOf("Versione", StringComparison.Ordinal);
                    if (versionStart < 0)
                    {
                        versionStart = line.IndexOf("Version", StringComparison.Ordinal);
                    }

                    availableStart = line.IndexOf("Disponibile", StringComparison.Ordinal);
                    if (availableStart < 0)
                    {
                        availableStart = line.IndexOf("Available", StringComparison.Ordinal);
                    }

                    if (idStart >= 0 && versionStart >= 0 && availableStart >= 0)
                    {
                        headerFound = true;
                    }
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(line) || line.Trim().All(c => c == '-' || c == ' '))
            {
                continue;
            }

            if (line.Length < availableStart || idStart <= nameStart || versionStart <= idStart || availableStart <= versionStart)
            {
                continue;
            }

            var name = line.Substring(nameStart, Math.Min(idStart - nameStart, line.Length)).Trim();
            var idEnd = versionStart > idStart ? versionStart - idStart : line.Length - idStart;
            var id = line.Substring(idStart, Math.Min(idEnd, line.Length - idStart)).Trim();
            var versionEnd = availableStart > versionStart ? availableStart - versionStart : line.Length - versionStart;
            var version = line.Substring(versionStart, Math.Min(versionEnd, line.Length - versionStart)).Trim();
            var available = line.Length > availableStart
                ? line.Substring(availableStart).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty
                : string.Empty;

            if (!string.IsNullOrWhiteSpace(id) && !id.Any(char.IsWhiteSpace))
            {
                results.Add(new UpdateEntry
                {
                    Name = name,
                    Id = id,
                    Version = version,
                    Available = available,
                    Selected = true
                });
            }
        }

        return results;
    }

    private static IReadOnlyList<UpdateEntry> ParseInstalledOutput(string output)
    {
        var results = new List<UpdateEntry>();
        var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var headerFound = false;
        var nameStart = 0;
        var idStart = 0;
        var versionStart = 0;

        foreach (var line in lines)
        {
            if (!headerFound)
            {
                if (line.StartsWith("Nome", StringComparison.OrdinalIgnoreCase) || line.StartsWith("Name", StringComparison.OrdinalIgnoreCase))
                {
                    idStart = line.IndexOf("ID", StringComparison.Ordinal);
                    if (idStart < 0)
                    {
                        idStart = line.IndexOf("Id", StringComparison.Ordinal);
                    }

                    versionStart = line.IndexOf("Versione", StringComparison.Ordinal);
                    if (versionStart < 0)
                    {
                        versionStart = line.IndexOf("Version", StringComparison.Ordinal);
                    }

                    if (idStart >= 0 && versionStart >= 0)
                    {
                        headerFound = true;
                    }
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(line) || line.Trim().All(c => c == '-' || c == ' '))
            {
                continue;
            }

            if (line.Length < versionStart || idStart <= nameStart)
            {
                continue;
            }

            var name = line.Substring(nameStart, Math.Min(idStart - nameStart, line.Length)).Trim();
            var idEnd = versionStart > idStart ? versionStart - idStart : line.Length - idStart;
            var id = line.Substring(idStart, Math.Min(idEnd, line.Length - idStart)).Trim();
            var version = line.Length > versionStart
                ? line.Substring(versionStart).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty
                : string.Empty;

            if (!string.IsNullOrWhiteSpace(id) && !id.Any(char.IsWhiteSpace))
            {
                results.Add(new UpdateEntry
                {
                    Name = name,
                    Id = id,
                    Version = version,
                    Available = string.Empty,
                    Selected = false
                });
            }
        }

        return results;
    }

    private static (int ExitCode, string Output) RunWinget(string? singleArg, params string[] args)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "winget",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (!string.IsNullOrWhiteSpace(singleArg))
        {
            processStartInfo.ArgumentList.Add(singleArg);
        }

        foreach (var arg in args)
        {
            processStartInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(processStartInfo);
        if (process == null)
        {
            return (9999, "Errore esecuzione");
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        var combined = string.IsNullOrWhiteSpace(error) ? output : output + Environment.NewLine + error;
        return (process.ExitCode, combined.Trim());
    }
}
