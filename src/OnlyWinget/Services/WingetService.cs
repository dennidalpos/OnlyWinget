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
using System.Threading;
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
    private const int AppInUseExitCode = -1978334975;
    private const int MaxInstallerDiagnosticLines = 8;
    private const int WideConsoleWidth = 500;
    private static readonly TimeSpan DefaultProcessTimeout = TimeSpan.FromHours(4);
    private readonly Func<string?, IReadOnlyList<string>, Action<string>?, CancellationToken, WingetCommandResult> _wingetRunner;
    private readonly WingetRuntimeEnvironment _runtimeEnvironment;
    private readonly WingetOutputClassifier _outputClassifier;
    private readonly TimeSpan? _processTimeoutOverride;

    public WingetService(
        Func<string?, IReadOnlyList<string>, Action<string>?, WingetCommandResult>? wingetRunner = null,
        string? localRuntimeRoot = null,
        Func<DateTime>? utcNow = null,
        TimeSpan? processTimeout = null)
        : this(
            wingetRunner == null
                ? null
                : (singleArg, args, onOutputLine, _) => wingetRunner(singleArg, args, onOutputLine),
            localRuntimeRoot,
            utcNow,
            processTimeout,
            true)
    {
    }

    public WingetService(
        Func<string?, IReadOnlyList<string>, Action<string>?, CancellationToken, WingetCommandResult> wingetRunner,
        string? localRuntimeRoot = null,
        Func<DateTime>? utcNow = null,
        TimeSpan? processTimeout = null)
        : this((Func<string?, IReadOnlyList<string>, Action<string>?, CancellationToken, WingetCommandResult>?)wingetRunner, localRuntimeRoot, utcNow, processTimeout, true)
    {
    }

    private WingetService(
        Func<string?, IReadOnlyList<string>, Action<string>?, CancellationToken, WingetCommandResult>? wingetRunner,
        string? localRuntimeRoot,
        Func<DateTime>? utcNow,
        TimeSpan? processTimeout,
        bool _)
    {
        var runtimeRoot = localRuntimeRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OnlyWinget",
            "runtime");
        _processTimeoutOverride = processTimeout;
        if (_processTimeoutOverride.HasValue && _processTimeoutOverride.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(processTimeout), "Process timeout must be greater than zero.");
        }

        _wingetRunner = wingetRunner == null
            ? RunWingetProcess
            : wingetRunner;
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
        => Invoke(command, parameters, onOutputLine, CancellationToken.None);

    public WingetCommandResult Invoke(
        string command,
        Dictionary<string, string?> parameters,
        Action<string>? onOutputLine,
        CancellationToken cancellationToken)
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

        return RunWinget(null, args.ToArray(), onOutputLine, cancellationToken);
    }

    public WingetCommandResult Invoke(IReadOnlyList<string> args, Action<string>? onOutputLine = null)
    {
        return Invoke(args, onOutputLine, CancellationToken.None);
    }

    public WingetCommandResult Invoke(IReadOnlyList<string> args, Action<string>? onOutputLine, CancellationToken cancellationToken)
    {
        return RunWinget(null, args, onOutputLine, cancellationToken);
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

    public SavedPackageResolutionResult ResolveSavedPackage(
        string id,
        string name,
        string? source = "winget",
        CancellationToken cancellationToken = default)
    {
        var normalizedId = (id ?? string.Empty).Trim();
        var normalizedName = (name ?? string.Empty).Trim();
        var normalizedSource = AppEntry.NormalizeSource(source);
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return SavedPackageResolutionResult.Unresolved(normalizedId, normalizedName, normalizedSource);
        }

        var exactIdResult = Invoke("show", CreatePackageLookupParameters("--id", normalizedId, normalizedSource, exact: true), null, cancellationToken);
        if (exactIdResult.ExitCode == 0 && !IsAmbiguousPackageOutput(exactIdResult.Output))
        {
            return SavedPackageResolutionResult.Resolved(normalizedId, normalizedName, normalizedSource);
        }

        if (IsAmbiguousPackageOutput(exactIdResult.Output))
        {
            return SavedPackageResolutionResult.Ambiguous(normalizedId, normalizedName, normalizedSource);
        }

        var idSearchResolution = ResolveUniqueSearchCandidate(
            SearchPackages("--id", normalizedId, normalizedSource, cancellationToken),
            normalizedId,
            normalizedName,
            normalizedSource,
            candidate => string.Equals(candidate.Id, normalizedId, StringComparison.OrdinalIgnoreCase));
        if (idSearchResolution.Status != SavedPackageResolutionStatus.Unresolved)
        {
            return idSearchResolution;
        }

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return SavedPackageResolutionResult.Unresolved(normalizedId, normalizedName, normalizedSource);
        }

        return ResolveUniqueSearchCandidate(
            SearchPackages("--name", normalizedName, normalizedSource, cancellationToken),
            normalizedId,
            normalizedName,
            normalizedSource,
            candidate => string.Equals(candidate.Name, normalizedName, StringComparison.CurrentCultureIgnoreCase));
    }

    public IReadOnlyList<SearchResult> Search(string query, CancellationToken cancellationToken = default)
    {
        var result = Invoke("search", new Dictionary<string, string?>
        {
            ["--query"] = query,
            ["--accept-source-agreements"] = null
        }, null, cancellationToken);

        var parsedResults = WingetTableParser.ParseSearchResults(result.Output);
        if (!parsedResults.Any(NeedsSearchResultExpansion))
        {
            return parsedResults;
        }

        return parsedResults
            .Select(result => ExpandSearchResult(result, cancellationToken))
            .ToList();
    }

    private IReadOnlyList<SearchResult> SearchPackages(
        string option,
        string value,
        string source,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<SearchResult>();
        }

        var result = Invoke("search", CreatePackageLookupParameters(option, value, source, exact: false), null, cancellationToken);
        if (result.ExitCode != 0 || IsAmbiguousPackageOutput(result.Output))
        {
            return Array.Empty<SearchResult>();
        }

        return WingetTableParser.ParseSearchResults(result.Output)
            .Where(candidate => string.IsNullOrWhiteSpace(source) || string.Equals(candidate.Source, source, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static SavedPackageResolutionResult ResolveUniqueSearchCandidate(
        IReadOnlyList<SearchResult> candidates,
        string originalId,
        string originalName,
        string originalSource,
        Func<SearchResult, bool> preferredMatch)
    {
        if (candidates.Count == 0)
        {
            return SavedPackageResolutionResult.Unresolved(originalId, originalName, originalSource);
        }

        var preferredCandidates = candidates.Where(preferredMatch).ToList();
        var resolutionCandidates = preferredCandidates.Count > 0 ? preferredCandidates : candidates;
        if (resolutionCandidates.Count != 1)
        {
            return SavedPackageResolutionResult.Ambiguous(originalId, originalName, originalSource);
        }

        var candidate = resolutionCandidates[0];
        return SavedPackageResolutionResult.Resolved(candidate.Id, candidate.Name, candidate.Source);
    }

    private static Dictionary<string, string?> CreatePackageLookupParameters(
        string option,
        string value,
        string source,
        bool exact)
    {
        var parameters = new Dictionary<string, string?>
        {
            [option] = value,
            ["--accept-source-agreements"] = null,
            ["--disable-interactivity"] = null
        };

        if (exact)
        {
            parameters["--exact"] = null;
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            parameters["--source"] = source;
        }

        return parameters;
    }

    private static bool IsAmbiguousPackageOutput(string output)
    {
        return output.Contains("Multiple packages found", StringComparison.OrdinalIgnoreCase)
            || output.Contains("Piu pacchetti trovati", StringComparison.OrdinalIgnoreCase)
            || output.Contains("Più pacchetti trovati", StringComparison.OrdinalIgnoreCase);
    }

    public IReadOnlyList<UpdateEntry> LoadUpdates(CancellationToken cancellationToken = default)
    {
        var updatesResult = Invoke("list", new Dictionary<string, string?>
        {
            ["--upgrade-available"] = null,
            ["--include-unknown"] = null,
            ["--include-pinned"] = null,
            ["--accept-source-agreements"] = null
        }, null, cancellationToken);

        var updates = WingetTableParser.ParseUpgradeEntries(updatesResult.Output);
        return updates
            .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public UpdateEntry? FindAvailableUpdate(string id, string? source = "winget", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var matchingUpdates = LoadUpdates(cancellationToken)
            .Where(update => string.Equals(update.Id, id, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matchingUpdates.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            var sourceMatch = matchingUpdates.FirstOrDefault(update => string.Equals(update.Source, source, StringComparison.OrdinalIgnoreCase));
            if (sourceMatch != null)
            {
                return sourceMatch;
            }
        }

        return matchingUpdates[0];
    }

    public WingetPackageDetails TryLoadInstalledPackageDetails(string id, string? source = "winget", CancellationToken cancellationToken = default)
    {
        try
        {
            return LoadInstalledPackageDetails(id, source, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new WingetPackageDetails();
        }
    }

    public WingetCommandResult UpgradeApp(
        string id,
        string? source = "winget",
        string? name = null,
        string? availableVersion = null,
        string? configuredScope = null,
        string? configuredArchitecture = null,
        string? configuredLocale = null,
        string? configuredInstallerType = null,
        Action<string>? onOutputLine = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = CreatePackageParameters("upgrade", id, source, includeLog: true);
        parameters["--include-pinned"] = null;
        var result = Invoke("upgrade", parameters, onOutputLine, cancellationToken);
        if (ShouldFallbackToInstall(result) && !string.IsNullOrWhiteSpace(name))
        {
            var nameParameters = CreatePackageNameParameters("upgrade-by-name", name, source, includeLog: true);
            nameParameters["--include-pinned"] = null;
            var nameResult = Invoke("upgrade", nameParameters, onOutputLine, cancellationToken);
            var nameLog = new List<string>();
            nameLog.AddRange(FormatCommandSummary("upgrade", parameters, result));
            nameLog.Add("retrying with installed package name");
            nameLog.AddRange(FormatCommandSummary("upgrade", nameParameters, nameResult));
            return CombineResults(nameResult, nameLog);
        }

        if (!IsNoApplicableUpgrade(result))
        {
            return ToDisplayResult("upgrade", parameters, result);
        }

        var retryParameters = CreateNoApplicableUpgradeRetryParameters(
            id,
            source,
            availableVersion,
            configuredScope,
            configuredArchitecture,
            configuredLocale,
            configuredInstallerType);
        if (retryParameters == null)
        {
            return ToDisplayResult("upgrade", parameters, result);
        }

        var retryResult = Invoke("upgrade", retryParameters, onOutputLine, cancellationToken);
        var log = new List<string>();
        log.AddRange(FormatCommandSummary("upgrade", parameters, result));
        log.Add("retrying with installed package requirements");
        log.AddRange(FormatCommandSummary("upgrade", retryParameters, retryResult));
        return CombineResults(retryResult, log);
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

    public WingetCommandResult UpdateSources()
    {
        return Invoke(new[] { "source", "update" });
    }

    public void CleanupOldLogs()
    {
        _runtimeEnvironment.CleanupOldLogs();
    }

    public bool IsNoUpgradeNeeded(int exitCode) => _outputClassifier.IsNoUpgradeNeeded(exitCode);

    public bool IsNoApplicableUpgrade(WingetCommandResult result) => _outputClassifier.IsNoApplicableUpgrade(result);

    public bool IsNoApplicableInstaller(WingetCommandResult result) => _outputClassifier.IsNoApplicableInstaller(result);

    public bool IsManifestNotFound(WingetCommandResult result) => _outputClassifier.IsManifestNotFound(result);

    public bool IsAlreadyInstalled(int exitCode) => _outputClassifier.IsAlreadyInstalled(exitCode);

    public bool IsAlreadyInstalled(WingetCommandResult result) => _outputClassifier.IsAlreadyInstalled(result);

    public string CreateOperationLogPath(string operation, string id) => _runtimeEnvironment.CreateOperationLogPath(operation, id);

    public string LogDirectory => _runtimeEnvironment.LogDirectory;

    private WingetCommandResult RunWinget(string? singleArg, IReadOnlyList<string> args, Action<string>? onOutputLine)
        => RunWinget(singleArg, args, onOutputLine, CancellationToken.None);

    private WingetCommandResult RunWinget(string? singleArg, IReadOnlyList<string> args, Action<string>? onOutputLine, CancellationToken cancellationToken)
        => _wingetRunner(singleArg, args, onOutputLine, cancellationToken);

    private WingetCommandResult RunWingetProcess(string? singleArg, IReadOnlyList<string> args, Action<string>? onOutputLine, CancellationToken cancellationToken)
        => RunWingetProcessAsync(singleArg, args, onOutputLine, cancellationToken).GetAwaiter().GetResult();

    private async Task<WingetCommandResult> RunWingetProcessAsync(
        string? singleArg,
        IReadOnlyList<string> args,
        Action<string>? onOutputLine,
        CancellationToken cancellationToken)
    {
        var runtimeDirectory = _runtimeEnvironment.EnsureLocalRuntimeDirectory();
        var commandArgs = BuildCommandArgs(singleArg, args);
        var processStartInfo = CreateProcessStartInfo(runtimeDirectory, commandArgs);
        var processTimeout = GetProcessTimeout(commandArgs);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(processTimeout);

        using var process = Process.Start(processStartInfo);
        if (process == null)
        {
            return new WingetCommandResult { ExitCode = 9999, Output = GetErrorMessage(9999) };
        }

        var output = new List<string>();
        var error = new List<string>();

        var outputTask = ReadStreamAsync(process.StandardOutput, output, onOutputLine, timeoutCts.Token);
        var errorTask = ReadStreamAsync(process.StandardError, error, onOutputLine, timeoutCts.Token);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            KillProcessTree(process);
            return new WingetCommandResult
            {
                ExitCode = 9997,
                Output = "event=winget_process_cancelled reason=cancellation_requested"
            };
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(process);
            return new WingetCommandResult
            {
                ExitCode = 9998,
                Output = $"event=winget_process_timeout timeout_seconds={(int)processTimeout.TotalSeconds}"
            };
        }

        var outputText = string.Join(Environment.NewLine, output);
        var errorText = string.Join(Environment.NewLine, error);
        var combined = string.IsNullOrEmpty(errorText)
            ? outputText
            : string.IsNullOrEmpty(outputText)
                ? errorText
                : outputText + Environment.NewLine + errorText;
        return new WingetCommandResult { ExitCode = process.ExitCode, Output = combined };
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // The process may have exited between cancellation and cleanup.
        }
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

    private TimeSpan GetProcessTimeout(IReadOnlyList<string> commandArgs)
    {
        if (_processTimeoutOverride.HasValue)
        {
            return _processTimeoutOverride.Value;
        }

        if (commandArgs.Count == 0)
        {
            return DefaultProcessTimeout;
        }

        return commandArgs[0].ToLowerInvariant() switch
        {
            "install" or "upgrade" or "uninstall" => TimeSpan.FromMinutes(90),
            "source" => TimeSpan.FromMinutes(5),
            "show" or "search" or "list" => TimeSpan.FromMinutes(2),
            _ => TimeSpan.FromMinutes(10)
        };
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

    private SearchResult ExpandSearchResult(SearchResult result, CancellationToken cancellationToken)
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

        var expandedResults = WingetTableParser.ParseSearchResults(Invoke("search", parameters, null, cancellationToken).Output);
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

    private static async Task ReadStreamAsync(StreamReader reader, ICollection<string> target, Action<string>? onOutputLine, CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
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

    private Dictionary<string, string?> CreatePackageNameParameters(
        string operation,
        string name,
        string? source = null,
        bool includeLog = false)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["--name"] = name
        };

        if (!string.IsNullOrWhiteSpace(source))
        {
            parameters["--source"] = source;
        }

        if (includeLog)
        {
            parameters["--log"] = _runtimeEnvironment.CreateOperationLogPath(operation, name);
        }

        parameters["--accept-package-agreements"] = null;
        parameters["--accept-source-agreements"] = null;
        parameters["--disable-interactivity"] = null;
        return parameters;
    }

    private Dictionary<string, string?>? CreateNoApplicableUpgradeRetryParameters(
        string id,
        string? source,
        string? availableVersion,
        string? configuredScope,
        string? configuredArchitecture,
        string? configuredLocale,
        string? configuredInstallerType)
    {
        if (string.IsNullOrWhiteSpace(availableVersion))
        {
            return null;
        }

        var installedDetails = LoadInstalledPackageDetails(id, source);
        var installerDetails = LoadInstallerDetails(id, source, availableVersion);
        var parameters = CreatePackageParameters("upgrade-retry", id, source, includeLog: true);
        parameters["--include-pinned"] = null;

        var addedConstraint = false;
        if (TryNormalizeScope(configuredScope, out var scope) || TryNormalizeScope(installedDetails.Scope, out scope))
        {
            parameters["--scope"] = scope;
            addedConstraint = true;
        }

        if (TryNormalizeArchitecture(configuredArchitecture, out var architecture) || TryNormalizeArchitecture(installedDetails.Architecture, out architecture))
        {
            parameters["--architecture"] = architecture;
            addedConstraint = true;
        }

        if (!string.IsNullOrWhiteSpace(configuredLocale))
        {
            parameters["--locale"] = configuredLocale.Trim();
            addedConstraint = true;
        }
        else if (!string.IsNullOrWhiteSpace(installerDetails.Locale))
        {
            parameters["--locale"] = installerDetails.Locale;
            addedConstraint = true;
        }

        if (!string.IsNullOrWhiteSpace(configuredInstallerType))
        {
            parameters["--installer-type"] = configuredInstallerType.Trim();
            addedConstraint = true;
        }

        return addedConstraint ? parameters : null;
    }

    private WingetPackageDetails LoadInstalledPackageDetails(string id, string? source, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["--id"] = id,
            ["--exact"] = null,
            ["--details"] = null,
            ["--accept-source-agreements"] = null
        };

        if (!string.IsNullOrWhiteSpace(source))
        {
            parameters["--source"] = source;
        }

        var result = Invoke("list", parameters, null, cancellationToken);
        return ParsePackageDetails(result.Output);
    }

    private WingetPackageDetails LoadInstallerDetails(string id, string? source, string availableVersion)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["--id"] = id,
            ["--exact"] = null,
            ["--version"] = availableVersion,
            ["--accept-source-agreements"] = null
        };

        if (!string.IsNullOrWhiteSpace(source))
        {
            parameters["--source"] = source;
        }

        var result = Invoke("show", parameters);
        return ParsePackageDetails(result.Output);
    }

    private static WingetPackageDetails ParsePackageDetails(string output)
    {
        var scope = string.Empty;
        var architecture = string.Empty;
        var locale = string.Empty;
        var installerType = string.Empty;
        foreach (var rawLine in output.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            var separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            if (MatchesAny(key, "Installed Scope", "Scope", "Ambito installato", "Ambito"))
            {
                scope = NormalizeScopeValue(value);
            }
            else if (MatchesAny(key, "Installed Architecture", "Architecture", "Architettura installata", "Architettura"))
            {
                architecture = NormalizeArchitectureValue(value);
            }
            else if (MatchesAny(key, "Installer Locale", "Locale programma di installazione"))
            {
                locale = value.Trim();
            }
            else if (MatchesAny(key, "Installer Type", "Tipo di programma di installazione"))
            {
                installerType = value.Trim();
            }
        }

        return new WingetPackageDetails
        {
            Scope = scope,
            Architecture = architecture,
            Locale = locale,
            InstallerType = installerType
        };
    }

    private static bool TryNormalizeScope(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (string.Equals(value, "Machine", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "machine";
            return true;
        }

        if (string.Equals(value, "User", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "user";
            return true;
        }

        return false;
    }

    private static bool TryNormalizeArchitecture(string? value, out string normalized)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            normalized = string.Empty;
            return false;
        }

        normalized = value.Trim().ToLowerInvariant();
        return normalized is "x64" or "x86" or "arm64" or "arm";
    }

    private static bool MatchesAny(string value, params string[] candidates)
    {
        return candidates.Any(candidate => string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeScopeValue(string value)
    {
        return TryNormalizeScope(value, out var normalized) ? normalized : value.Trim();
    }

    private static string NormalizeArchitectureValue(string value)
    {
        return TryNormalizeArchitecture(value, out var normalized) ? normalized : value.Trim();
    }

    private IEnumerable<string> FormatCommandSummary(string command, IReadOnlyDictionary<string, string?> parameters, WingetCommandResult result)
    {
        var id = parameters.TryGetValue("--id", out var value) ? value : string.Empty;
        var source = parameters.TryGetValue("--source", out var sourceValue) ? sourceValue : null;
        var target = string.IsNullOrWhiteSpace(source) ? id : $"{id} @ {source}";

        yield return $"{command}: {target}";

        var relevantOutputLines = _outputClassifier.GetRelevantOutputLines(result.Output);
        foreach (var line in relevantOutputLines)
        {
            yield return $"  {line}";
        }

        if (result.ExitCode != 0 && relevantOutputLines.Count == 0)
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
        var displayResult = CombineResults(result, FormatCommandSummary(command, parameters, result).ToList());
        return TryCreateInstallerDiagnosticResult(parameters, displayResult, out var diagnosticResult)
            ? diagnosticResult
            : displayResult;
    }

    private static bool TryCreateInstallerDiagnosticResult(
        IReadOnlyDictionary<string, string?> parameters,
        WingetCommandResult result,
        out WingetCommandResult diagnosticResult)
    {
        diagnosticResult = result;
        if (!parameters.TryGetValue("--log", out var logPath) || string.IsNullOrWhiteSpace(logPath))
        {
            return false;
        }

        if (!TryReadPackageInUseDiagnostics(logPath, out var diagnosticLines))
        {
            return false;
        }

        diagnosticResult = new WingetCommandResult
        {
            ExitCode = AppInUseExitCode,
            Output = AppendDiagnosticLines(result.Output, diagnosticLines)
        };
        return true;
    }

    private static bool TryReadPackageInUseDiagnostics(string logPath, out IReadOnlyList<string> diagnosticLines)
    {
        diagnosticLines = Array.Empty<string>();
        try
        {
            if (!File.Exists(logPath))
            {
                return false;
            }

            var matches = File.ReadLines(logPath)
                .Where(IsPackageInUseLogLine)
                .Take(MaxInstallerDiagnosticLines)
                .Select(line => $"  {TrimDiagnosticLine(line)}")
                .ToList();
            if (matches.Count == 0)
            {
                return false;
            }

            matches.Insert(0, $"Installer log: {logPath}");
            diagnosticLines = matches;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsPackageInUseLogLine(string line)
    {
        return line.Contains("0x80073D02", StringComparison.OrdinalIgnoreCase)
            || line.Contains("close the following apps", StringComparison.OrdinalIgnoreCase)
            || line.Contains("chiudere le app seguenti", StringComparison.OrdinalIgnoreCase);
    }

    private static string TrimDiagnosticLine(string line)
    {
        var trimmed = line.Trim();
        const int maxLength = 500;
        return trimmed.Length <= maxLength
            ? trimmed
            : trimmed[..maxLength] + "...";
    }

    private static string AppendDiagnosticLines(string output, IReadOnlyList<string> diagnosticLines)
    {
        if (diagnosticLines.Count == 0)
        {
            return output;
        }

        var diagnosticText = string.Join(Environment.NewLine, diagnosticLines);
        return string.IsNullOrWhiteSpace(output)
            ? diagnosticText
            : output + Environment.NewLine + diagnosticText;
    }

    public bool ShouldFallbackToInstall(WingetCommandResult result) => _outputClassifier.ShouldFallbackToInstall(result);

    public IReadOnlyList<string> GetRelevantOutputLines(string output) => _outputClassifier.GetRelevantOutputLines(output);

    public bool TryGetProgressPercentage(string line, out int percentage) => _outputClassifier.TryGetProgressPercentage(line, out percentage);

    public bool ShouldLogOutputLine(string line) => _outputClassifier.ShouldLogOutputLine(line);

    public string GetErrorMessage(int exitCode, string? localeCode = null) => _outputClassifier.GetErrorMessage(exitCode, localeCode ?? System.Globalization.CultureInfo.CurrentUICulture.Name);

    public string GetResolutionHint(int exitCode, string? localeCode = null) => _outputClassifier.GetResolutionHint(exitCode, localeCode ?? System.Globalization.CultureInfo.CurrentUICulture.Name);

}
