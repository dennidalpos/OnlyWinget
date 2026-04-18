// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OnlyWinget.Models;

namespace OnlyWinget.Services;

public sealed class WingetVersionCheckResult
{
    public string InstalledVersion { get; init; } = string.Empty;
    public string LatestVersion { get; init; } = string.Empty;
    public bool IsUpdateAvailable { get; init; }
}

public sealed class WingetCommandResult
{
    public int ExitCode { get; init; }
    public string Output { get; init; } = string.Empty;
}

public sealed class WingetService
{
    private const string WingetPackageId = "Microsoft.AppInstaller";
    private const int WideConsoleWidth = 500;
    private readonly Func<string?, IReadOnlyList<string>, Action<string>?, WingetCommandResult> _wingetRunner;
    private readonly WingetRuntimeEnvironment _runtimeEnvironment;
    private readonly WingetOutputClassifier _outputClassifier;

    public WingetService(
        Func<string?, IReadOnlyList<string>, Action<string>?, WingetCommandResult>? wingetRunner = null,
        string? localRuntimeRoot = null,
        Func<DateTime>? utcNow = null)
    {
        var runtimeRoot = localRuntimeRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OnlyWinget",
            "runtime");
        _wingetRunner = wingetRunner ?? RunWingetProcess;
        _runtimeEnvironment = new WingetRuntimeEnvironment(runtimeRoot, utcNow ?? (() => DateTime.UtcNow));
        _outputClassifier = new WingetOutputClassifier();
    }

    public bool TestAvailable()
    {
        try
        {
            var result = RunWinget("--version", Array.Empty<string>(), null);
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public string GetInstalledWingetVersion()
    {
        try
        {
            var result = RunWinget("--version", Array.Empty<string>(), null);
            if (result.ExitCode != 0)
            {
                return string.Empty;
            }

            var firstLine = result.Output
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?.Trim() ?? string.Empty;
            return firstLine.TrimStart('v', 'V');
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task<WingetVersionCheckResult> CheckForWingetUpdateAsync()
    {
        var installed = GetInstalledWingetVersion();

        var result = await Task.Run(() => Invoke("upgrade", new Dictionary<string, string?>
        {
            ["--id"] = WingetPackageId,
            ["--exact"] = null,
            ["--include-unknown"] = null,
            ["--accept-source-agreements"] = null,
            ["--disable-interactivity"] = null
        })).ConfigureAwait(false);

        var availableUpdate = WingetTableParser.ParseUpgradeEntries(result.Output)
            .FirstOrDefault(entry => string.Equals(entry.Id, WingetPackageId, StringComparison.OrdinalIgnoreCase));

        var latest = availableUpdate?.Available ?? string.Empty;
        var updateAvailable = availableUpdate != null
            && !string.IsNullOrWhiteSpace(latest)
            && !string.Equals(installed, latest, StringComparison.OrdinalIgnoreCase);

        return new WingetVersionCheckResult
        {
            InstalledVersion = installed,
            LatestVersion = latest,
            IsUpdateAvailable = updateAvailable
        };
    }

    public WingetCommandResult Invoke(string command, Dictionary<string, string?> parameters, Action<string>? onOutputLine = null)
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

        return RunWinget(null, args.ToArray(), onOutputLine);
    }

    public WingetCommandResult Invoke(IReadOnlyList<string> args, Action<string>? onOutputLine = null)
    {
        return RunWinget(null, args, onOutputLine);
    }

    public bool TestAppExists(string id, string source = "winget")
    {
        var parameters = new Dictionary<string, string?>
        {
            ["--id"] = id,
            ["--exact"] = null,
            ["--accept-source-agreements"] = null
        };

        if (!string.IsNullOrWhiteSpace(source))
        {
            parameters["--source"] = source;
        }

        var result = Invoke("show", parameters);
        return result.ExitCode == 0;
    }

    public IReadOnlyList<SearchResult> Search(string query)
    {
        var result = Invoke("search", new Dictionary<string, string?>
        {
            ["--query"] = query,
            ["--accept-source-agreements"] = null
        });

        var parsedResults = WingetTableParser.ParseSearchResults(result.Output);
        if (!parsedResults.Any(NeedsSearchResultExpansion))
        {
            return parsedResults;
        }

        return parsedResults
            .Select(ExpandSearchResult)
            .ToList();
    }

    public IReadOnlyList<UpdateEntry> LoadUpdates()
    {
        var updatesResult = Invoke("list", new Dictionary<string, string?>
        {
            ["--upgrade-available"] = null,
            ["--include-unknown"] = null,
            ["--include-pinned"] = null,
            ["--accept-source-agreements"] = null
        });

        var updates = WingetTableParser.ParseUpgradeEntries(updatesResult.Output);
        if (updates.Count > 0 || updatesResult.ExitCode == 0 || IsNoUpgradeNeeded(updatesResult.ExitCode))
        {
            return updates
                .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        return LoadUpdatesLegacy();
    }

    private IReadOnlyList<UpdateEntry> LoadUpdatesLegacy()
    {
        var upgradesResult = Invoke("upgrade", new Dictionary<string, string?>
        {
            ["--accept-source-agreements"] = null
        });

        return WingetTableParser.ParseUpgradeEntries(upgradesResult.Output)
            .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public WingetCommandResult UpgradeApp(string id, string? source = "winget", Action<string>? onOutputLine = null)
    {
        var parameters = CreatePackageParameters("upgrade", id, source, includeLog: true);
        var result = Invoke("upgrade", parameters, onOutputLine);
        return ToDisplayResult("upgrade", parameters, result);
    }

    public WingetCommandResult UpgradeWinget()
    {
        var attempts = new List<(string Command, Dictionary<string, string?> Parameters)>
        {
            ("upgrade", CreatePackageParameters("upgrade-winget", WingetPackageId, "winget", includeLog: true)),
            ("upgrade", CreatePackageParameters("upgrade-winget", WingetPackageId, null, includeLog: true)),
            ("upgrade", CreatePackageParameters("upgrade-winget", WingetPackageId, "msstore", includeLog: true))
        };

        var log = new List<string>();
        WingetCommandResult? lastResult = null;
        var shouldAttemptInstall = false;

        foreach (var attempt in attempts)
        {
            var result = Invoke(attempt.Command, attempt.Parameters);
            lastResult = result;
            log.AddRange(FormatCommandSummary(attempt.Command, attempt.Parameters, result));

            if (result.ExitCode == 0 || IsNoUpgradeNeeded(result.ExitCode))
            {
                return CombineResults(result, log);
            }

            if (ShouldFallbackToInstall(result))
            {
                shouldAttemptInstall = true;
                break;
            }
        }

        if (!shouldAttemptInstall)
        {
            return lastResult is null
                ? new WingetCommandResult { ExitCode = 9999, Output = GetErrorMessage(9999) }
                : CombineResults(lastResult, log);
        }

        foreach (var source in new[] { "winget", null, "msstore" })
        {
            var parameters = CreatePackageParameters("install-winget", WingetPackageId, source, includeLog: true);
            var result = Invoke("install", parameters);
            lastResult = result;
            log.AddRange(FormatCommandSummary("install", parameters, result));

            if (result.ExitCode == 0 || IsNoUpgradeNeeded(result.ExitCode))
            {
                return CombineResults(result, log);
            }

            if (IsAlreadyInstalled(result))
            {
                return CombineResults(result, log);
            }
        }

        return lastResult is null
            ? new WingetCommandResult { ExitCode = 9999, Output = GetErrorMessage(9999) }
            : CombineResults(lastResult, log);
    }

    public WingetCommandResult InstallApp(string id, Action<string>? onOutputLine = null)
    {
        var parameters = CreatePackageParameters("install", id, includeLog: true);
        var result = Invoke("install", parameters, onOutputLine);
        return ToDisplayResult("install", parameters, result);
    }

    public WingetCommandResult UninstallApp(string id, Action<string>? onOutputLine = null)
    {
        var parameters = CreatePackageParameters("uninstall", id, includeLog: true, includePackageAgreements: false);
        var result = Invoke("uninstall", parameters, onOutputLine);
        return ToDisplayResult("uninstall", parameters, result);
    }

    public void CleanupLocalTemp()
    {
        _runtimeEnvironment.CleanupLocalTemp();
    }

    public bool IsNoUpgradeNeeded(int exitCode) => _outputClassifier.IsNoUpgradeNeeded(exitCode);

    public bool IsAlreadyInstalled(int exitCode) => _outputClassifier.IsAlreadyInstalled(exitCode);

    public bool IsAlreadyInstalled(WingetCommandResult result) => _outputClassifier.IsAlreadyInstalled(result);

    public string CreateOperationLogPath(string operation, string id) => _runtimeEnvironment.CreateOperationLogPath(operation, id);

    public string LogDirectory => _runtimeEnvironment.LogDirectory;

    private WingetCommandResult RunWinget(string? singleArg, IReadOnlyList<string> args, Action<string>? onOutputLine)
        => _wingetRunner(singleArg, args, onOutputLine);

    private WingetCommandResult RunWingetProcess(string? singleArg, IReadOnlyList<string> args, Action<string>? onOutputLine)
    {
        var runtimeDirectory = _runtimeEnvironment.EnsureLocalRuntimeDirectory();
        var commandArgs = BuildCommandArgs(singleArg, args);
        var processStartInfo = CreateProcessStartInfo(runtimeDirectory, commandArgs);

        using var process = Process.Start(processStartInfo);
        if (process == null)
        {
            return new WingetCommandResult { ExitCode = 9999, Output = GetErrorMessage(9999) };
        }

        var output = new List<string>();
        var error = new List<string>();

        var outputTask = Task.Run(async () => await ReadStreamAsync(process.StandardOutput, output, onOutputLine));
        var errorTask = Task.Run(async () => await ReadStreamAsync(process.StandardError, error, onOutputLine));
        process.WaitForExit();
        Task.WaitAll(outputTask, errorTask);

        var outputText = string.Join(Environment.NewLine, output);
        var errorText = string.Join(Environment.NewLine, error);
        var combined = string.IsNullOrEmpty(errorText)
            ? outputText
            : string.IsNullOrEmpty(outputText)
                ? errorText
                : outputText + Environment.NewLine + errorText;
        return new WingetCommandResult { ExitCode = process.ExitCode, Output = combined };
    }

    private static IReadOnlyList<string> BuildCommandArgs(string? singleArg, IReadOnlyList<string> args)
    {
        if (string.IsNullOrWhiteSpace(singleArg))
        {
            return args;
        }

        var commandArgs = new List<string>(capacity: args.Count + 1)
        {
            singleArg
        };
        commandArgs.AddRange(args);
        return commandArgs;
    }

    private static ProcessStartInfo CreateProcessStartInfo(string runtimeDirectory, IReadOnlyList<string> commandArgs)
    {
        var processStartInfo = ShouldUseWideConsole(commandArgs)
            ? CreateWideConsoleProcessStartInfo(runtimeDirectory, commandArgs)
            : CreateDirectWingetProcessStartInfo(runtimeDirectory, commandArgs);

        processStartInfo.Environment["TMP"] = runtimeDirectory;
        processStartInfo.Environment["TEMP"] = runtimeDirectory;
        return processStartInfo;
    }

    private static ProcessStartInfo CreateDirectWingetProcessStartInfo(string runtimeDirectory, IReadOnlyList<string> commandArgs)
    {
        var processStartInfo = CreateBaseProcessStartInfo("winget", runtimeDirectory);
        foreach (var arg in commandArgs)
        {
            processStartInfo.ArgumentList.Add(arg);
        }

        return processStartInfo;
    }

    private static ProcessStartInfo CreateWideConsoleProcessStartInfo(string runtimeDirectory, IReadOnlyList<string> commandArgs)
    {
        var processStartInfo = CreateBaseProcessStartInfo("powershell.exe", runtimeDirectory);
        processStartInfo.ArgumentList.Add("-NoLogo");
        processStartInfo.ArgumentList.Add("-NoProfile");
        processStartInfo.ArgumentList.Add("-NonInteractive");
        processStartInfo.ArgumentList.Add("-Command");
        processStartInfo.ArgumentList.Add(BuildWideConsoleWingetCommand(commandArgs));
        return processStartInfo;
    }

    private static ProcessStartInfo CreateBaseProcessStartInfo(string fileName, string runtimeDirectory)
    {
        return new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = runtimeDirectory
        };
    }

    private static bool ShouldUseWideConsole(IReadOnlyList<string> commandArgs)
    {
        if (commandArgs.Count == 0)
        {
            return false;
        }

        var command = commandArgs[0];
        if (string.Equals(command, "search", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command, "list", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(command, "upgrade", StringComparison.OrdinalIgnoreCase) &&
            !commandArgs.Any(arg => string.Equals(arg, "--log", StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildWideConsoleWingetCommand(IReadOnlyList<string> commandArgs)
    {
        var escapedArgs = string.Join(
            ", ",
            commandArgs.Select(static arg => $"'{EscapePowerShellLiteral(arg)}'"));

        return string.Join(
            "; ",
            "$rawUi = $Host.UI.RawUI",
            "if ($null -ne $rawUi) { try { $rawUi.BufferSize = New-Object Management.Automation.Host.Size(" + WideConsoleWidth + ", $rawUi.BufferSize.Height) } catch { } }",
            "& winget @(" + escapedArgs + ")",
            "exit $LASTEXITCODE");
    }

    private static string EscapePowerShellLiteral(string value)
        => value.Replace("'", "''", StringComparison.Ordinal);

    private SearchResult ExpandSearchResult(SearchResult result)
    {
        if (!NeedsSearchResultExpansion(result))
        {
            return result;
        }

        var idQuery = GetSearchLookupPrefix(result.Id);
        if (string.IsNullOrWhiteSpace(idQuery))
        {
            return result;
        }

        var parameters = new Dictionary<string, string?>
        {
            ["--id"] = idQuery,
            ["--accept-source-agreements"] = null,
            ["--disable-interactivity"] = null
        };

        if (!string.IsNullOrWhiteSpace(result.Source))
        {
            parameters["--source"] = result.Source;
        }

        var expandedResults = WingetTableParser.ParseSearchResults(Invoke("search", parameters).Output);
        var expandedResult = expandedResults.FirstOrDefault(candidate => MatchesExpandedSearchResult(result, candidate, idQuery));
        return expandedResult ?? result;
    }

    private static bool NeedsSearchResultExpansion(SearchResult result)
    {
        return HasTruncationMarker(result.Name) || HasTruncationMarker(result.Id);
    }

    private static bool HasTruncationMarker(string value)
    {
        return value.Contains('…') || value.Contains("...", StringComparison.Ordinal);
    }

    private static string GetSearchLookupPrefix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var ellipsisIndex = value.IndexOf('…');
        if (ellipsisIndex >= 0)
        {
            return value[..ellipsisIndex];
        }

        var dotsIndex = value.IndexOf("...", StringComparison.Ordinal);
        return dotsIndex >= 0
            ? value[..dotsIndex]
            : value;
    }

    private static bool MatchesExpandedSearchResult(SearchResult original, SearchResult candidate, string idQuery)
    {
        if (!candidate.Id.StartsWith(idQuery, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(original.Source) &&
            !string.Equals(candidate.Source, original.Source, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(original.Version) &&
            !string.Equals(original.Version, "Unknown", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(candidate.Version, original.Version, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var originalNamePrefix = GetSearchLookupPrefix(original.Name);
        if (!string.IsNullOrWhiteSpace(originalNamePrefix) &&
            !candidate.Name.StartsWith(originalNamePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static async Task ReadStreamAsync(StreamReader reader, ICollection<string> target, Action<string>? onOutputLine)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line == null)
            {
                break;
            }

            target.Add(line);
            onOutputLine?.Invoke(line);
        }
    }

    private Dictionary<string, string?> CreatePackageParameters(
        string operation,
        string id,
        string? source = null,
        bool includeLog = false,
        bool includePackageAgreements = true)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["--id"] = id,
            ["--exact"] = null
        };

        if (!string.IsNullOrWhiteSpace(source))
        {
            parameters["--source"] = source;
        }

        if (includeLog)
        {
            parameters["--log"] = _runtimeEnvironment.CreateOperationLogPath(operation, id);
        }

        if (includePackageAgreements)
        {
            parameters["--accept-package-agreements"] = null;
        }

        parameters["--accept-source-agreements"] = null;
        parameters["--disable-interactivity"] = null;
        return parameters;
    }

    private IEnumerable<string> FormatCommandSummary(string command, IReadOnlyDictionary<string, string?> parameters, WingetCommandResult result)
    {
        var id = parameters.TryGetValue("--id", out var value) ? value : string.Empty;
        var source = parameters.TryGetValue("--source", out var sourceValue) ? sourceValue : null;
        var target = string.IsNullOrWhiteSpace(source) ? id : $"{id} @ {source}";

        yield return $"{command}: {target}";

        foreach (var line in _outputClassifier.GetRelevantOutputLines(result.Output))
        {
            yield return $"  {line}";
        }

        if (result.ExitCode != 0)
        {
            yield return $"  {GetErrorMessage(result.ExitCode)}";
        }
    }

    private static WingetCommandResult CombineResults(WingetCommandResult result, IReadOnlyList<string> lines)
    {
        var relevantLines = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
        var output = string.Join(Environment.NewLine, relevantLines);
        if (string.IsNullOrWhiteSpace(output))
        {
            output = result.Output;
        }

        return new WingetCommandResult
        {
            ExitCode = result.ExitCode,
            Output = output
        };
    }

    private WingetCommandResult ToDisplayResult(string command, IReadOnlyDictionary<string, string?> parameters, WingetCommandResult result)
    {
        return CombineResults(result, FormatCommandSummary(command, parameters, result).ToList());
    }

    public bool ShouldFallbackToInstall(WingetCommandResult result) => _outputClassifier.ShouldFallbackToInstall(result);

    public IReadOnlyList<string> GetRelevantOutputLines(string output) => _outputClassifier.GetRelevantOutputLines(output);

    public bool TryGetProgressPercentage(string line, out int percentage) => _outputClassifier.TryGetProgressPercentage(line, out percentage);

    public bool ShouldLogOutputLine(string line) => _outputClassifier.ShouldLogOutputLine(line);

    public string GetErrorMessage(int exitCode, string? localeCode = null) => _outputClassifier.GetErrorMessage(exitCode, localeCode ?? System.Globalization.CultureInfo.CurrentUICulture.Name);

    public string GetResolutionHint(int exitCode, string? localeCode = null) => _outputClassifier.GetResolutionHint(exitCode, localeCode ?? System.Globalization.CultureInfo.CurrentUICulture.Name);
}
