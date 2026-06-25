// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

public sealed class WingetCommandService
{
    private const string WingetPackageId = "Microsoft.AppInstaller";
    private const int AppInUseExitCode = -1978334975;
    private const int MaxInstallerDiagnosticLines = 8;
    private readonly IWingetCommandRunner _wingetRunner;
    private readonly WingetRuntimeEnvironment _runtimeEnvironment;
    private readonly WingetOutputClassifier _outputClassifier;

    public WingetCommandService(
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

    public WingetCommandService(
        Func<string?, IReadOnlyList<string>, Action<string>?, CancellationToken, WingetCommandResult> wingetRunner,
        string? localRuntimeRoot = null,
        Func<DateTime>? utcNow = null,
        TimeSpan? processTimeout = null)
        : this((Func<string?, IReadOnlyList<string>, Action<string>?, CancellationToken, WingetCommandResult>?)wingetRunner, localRuntimeRoot, utcNow, processTimeout, true)
    {
    }

    private WingetCommandService(
        Func<string?, IReadOnlyList<string>, Action<string>?, CancellationToken, WingetCommandResult>? wingetRunner,
        string? localRuntimeRoot,
        Func<DateTime>? utcNow,
        TimeSpan? processTimeout,
        bool _)
        : this(
            wingetRunner == null ? null : new DelegateWingetCommandRunner(wingetRunner),
            localRuntimeRoot,
            utcNow,
            processTimeout)
    {
    }

    public WingetCommandService(
        IWingetCommandRunner? wingetRunner,
        string? localRuntimeRoot = null,
        Func<DateTime>? utcNow = null,
        TimeSpan? processTimeout = null)
    {
        var runtimeRoot = localRuntimeRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OnlyWinget",
            "runtime");
        _runtimeEnvironment = new WingetRuntimeEnvironment(runtimeRoot, utcNow ?? (() => DateTime.UtcNow));
        _outputClassifier = new WingetOutputClassifier();
        _wingetRunner = wingetRunner ?? new WingetProcessRunner(_runtimeEnvironment, _outputClassifier, processTimeout);
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

        var result = await InvokeAsync("upgrade", new Dictionary<string, string?>
        {
            ["--id"] = WingetPackageId,
            ["--exact"] = null,
            ["--include-unknown"] = null,
            ["--accept-source-agreements"] = null,
            ["--disable-interactivity"] = null
        }, null, CancellationToken.None).ConfigureAwait(false);

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

    public Task<WingetCommandResult> InvokeAsync(
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

        return RunWingetAsync(null, args.ToArray(), onOutputLine, cancellationToken);
    }

    public WingetCommandResult Invoke(IReadOnlyList<string> args, Action<string>? onOutputLine = null)
    {
        return Invoke(args, onOutputLine, CancellationToken.None);
    }

    public WingetCommandResult Invoke(IReadOnlyList<string> args, Action<string>? onOutputLine, CancellationToken cancellationToken)
    {
        return RunWinget(null, args, onOutputLine, cancellationToken);
    }

    public Task<WingetCommandResult> InvokeAsync(IReadOnlyList<string> args, Action<string>? onOutputLine, CancellationToken cancellationToken)
    {
        return RunWingetAsync(null, args, onOutputLine, cancellationToken);
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
        return UpgradeAppAsync(
            id,
            source,
            name,
            availableVersion,
            configuredScope,
            configuredArchitecture,
            configuredLocale,
            configuredInstallerType,
            onOutputLine,
            cancellationToken).GetAwaiter().GetResult();
    }

    public async Task<WingetCommandResult> UpgradeAppAsync(
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
        var result = await InvokeAsync("upgrade", parameters, onOutputLine, cancellationToken).ConfigureAwait(false);
        if (ShouldFallbackToInstall(result) && !string.IsNullOrWhiteSpace(name))
        {
            var nameParameters = CreatePackageNameParameters("upgrade-by-name", name, source, includeLog: true);
            nameParameters["--include-pinned"] = null;
            var nameResult = await InvokeAsync("upgrade", nameParameters, onOutputLine, cancellationToken).ConfigureAwait(false);
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

        var retryResult = await InvokeAsync("upgrade", retryParameters, onOutputLine, cancellationToken).ConfigureAwait(false);
        var log = new List<string>();
        log.AddRange(FormatCommandSummary("upgrade", parameters, result));
        log.Add("retrying with installed package requirements");
        log.AddRange(FormatCommandSummary("upgrade", retryParameters, retryResult));
        return CombineResults(retryResult, log);
    }

    public WingetCommandResult UpgradeWinget()
    {
        return UpgradeWingetAsync().GetAwaiter().GetResult();
    }

    public async Task<WingetCommandResult> UpgradeWingetAsync(CancellationToken cancellationToken = default)
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
            var result = await InvokeAsync(attempt.Command, attempt.Parameters, null, cancellationToken).ConfigureAwait(false);
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
            var result = await InvokeAsync("install", parameters, null, cancellationToken).ConfigureAwait(false);
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
        return UpdateSourcesAsync().GetAwaiter().GetResult();
    }

    public Task<WingetCommandResult> UpdateSourcesAsync(Action<string>? onOutputLine = null, CancellationToken cancellationToken = default)
    {
        return InvokeAsync(new[] { "source", "update" }, onOutputLine, cancellationToken);
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
        => _wingetRunner.Run(singleArg, args, onOutputLine, cancellationToken);

    private Task<WingetCommandResult> RunWingetAsync(string? singleArg, IReadOnlyList<string> args, Action<string>? onOutputLine, CancellationToken cancellationToken)
        => _wingetRunner.RunAsync(singleArg, args, onOutputLine, cancellationToken);

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
