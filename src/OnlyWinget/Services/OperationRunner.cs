// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OnlyWinget.Models;

namespace OnlyWinget.Services;

public sealed class OperationRunner : IOperationRunner
{
    private const int IndeterminateProgress = -1;
    private readonly WingetService _wingetService;
    private readonly IInstallCommandBuilder _installCommandBuilder;
    private readonly ElevatedWingetLauncher _elevatedLauncher;
    private readonly bool _isCurrentProcessElevated;

    public OperationRunner(
        WingetService wingetService,
        IInstallCommandBuilder installCommandBuilder,
        ElevatedWingetLauncher? elevatedLauncher = null,
        bool? isCurrentProcessElevated = null)
    {
        _wingetService = wingetService;
        _installCommandBuilder = installCommandBuilder;
        _elevatedLauncher = elevatedLauncher ?? new ElevatedWingetLauncher();
        _isCurrentProcessElevated = isCurrentProcessElevated ?? ProcessElevationService.IsRunningAsAdministrator;
    }

    public async Task RunApplyAsync(
        IReadOnlyList<AppEntry> apps,
        Action<string, UiStatusState> setStatusById,
        Action<string> appendOutput,
        Action<int, string> reportProgress,
        LocalizedStrings strings,
        Action<string, string, string>? setErrorById = null,
        CancellationToken cancellationToken = default)
    {
        _wingetService.CleanupOldLogs();
        try
        {
            appendOutput($"=== {strings.OperationStartText} ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");
            reportProgress(0, strings.OperationStartText);

            for (var index = 0; index < apps.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var app = apps[index];
                var operationKey = app.OperationKey;
                if (string.IsNullOrWhiteSpace(app.Id))
                {
                    continue;
                }

                switch (app.Action)
                {
                    case AppActions.Pause:
                        setStatusById(operationKey, UiStatusState.FromKey(UiStatusKey.Paused));
                        setErrorById?.Invoke(operationKey, string.Empty, string.Empty);
                        reportProgress(CalculateOverallPercentage(index + 1, apps.Count), $"{app.Name}: 100%");
                        break;

                    case AppActions.Uninstall:
                        await RunUninstallAsync(app, index, apps.Count, setStatusById, appendOutput, reportProgress, strings, setErrorById, cancellationToken);
                        break;

                    default:
                        await RunInstallOrUpgradeAsync(app, index, apps.Count, setStatusById, appendOutput, reportProgress, strings, setErrorById, cancellationToken);
                        break;
                }
            }

            reportProgress(100, strings.OperationEndText);
            appendOutput($"=== {strings.OperationEndText} ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");
        }
        catch (OperationCanceledException)
        {
            appendOutput("event=apply_cancelled reason=cancellation_requested");
        }
        catch (Exception ex)
        {
            appendOutput($"event=apply_error message=\"{ex.Message}\"");
            throw;
        }
    }

    public async Task RunUpdatesAsync(
        IReadOnlyList<UpdateEntry> updates,
        Action<string, UiStatusState> setStatusById,
        Action<string> appendOutput,
        Action<int, string> reportProgress,
        LocalizedStrings strings,
        Action<string, string, string>? setErrorById = null,
        CancellationToken cancellationToken = default)
    {
        _wingetService.CleanupOldLogs();
        try
        {
            appendOutput($"=== {strings.UpdatesStartText} ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");
            reportProgress(0, strings.UpdatesStartText);

            for (var index = 0; index < updates.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var update = updates[index];
                setStatusById(update.Id, UiStatusState.FromKey(UiStatusKey.UpgradeInProgress));
                setErrorById?.Invoke(update.Id, string.Empty, string.Empty);
                appendOutput($"--- {update.Name} [{update.Id}] : {strings.OperationUpgradeLabel} ---");
                var receivedLiveOutput = false;
                var loggedOutputLines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var result = await Task.Run(() => _wingetService.UpgradeApp(
                    update.Id,
                    update.Source,
                    update.Name,
                    update.Available,
                    update.Scope,
                    update.Architecture,
                    update.Locale,
                    update.InstallerType,
                    line =>
                {
                    receivedLiveOutput = true;
                    HandleProgressLine(line, update.Id, update.Name, UiStatusKey.UpgradeInProgress, index, updates.Count, setStatusById, reportProgress);
                    if (_wingetService.ShouldLogOutputLine(line))
                    {
                        var trimmedLine = line.Trim();
                        loggedOutputLines.Add(trimmedLine);
                        appendOutput(trimmedLine);
                    }
                }, cancellationToken), cancellationToken);

                if (!receivedLiveOutput)
                {
                    AppendResultOutput(appendOutput, result);
                }

                if (result.ExitCode == 0)
                {
                    var stillAvailableUpdate = await Task.Run(
                        () => _wingetService.FindAvailableUpdate(update.Id, update.Source, cancellationToken),
                        cancellationToken);
                    if (stillAvailableUpdate != null)
                    {
                        var message = UpdateVerificationFormatter.FormatStillAvailableStatus(strings.LocaleCode);
                        var resolution = UpdateVerificationFormatter.FormatStillAvailableResolution(strings.LocaleCode, update, stillAvailableUpdate);
                        setStatusById(update.Id, UiStatusState.FromRawText(message));
                        setErrorById?.Invoke(update.Id, message, resolution);
                        appendOutput(UpdateVerificationFormatter.FormatStillAvailableLog(update, stillAvailableUpdate));
                    }
                    else
                    {
                        setStatusById(update.Id, UiStatusState.FromKey(UiStatusKey.Ok));
                    }
                }
                else if (_wingetService.IsNoUpgradeNeeded(result.ExitCode))
                {
                    if (receivedLiveOutput)
                    {
                        foreach (var line in _wingetService.GetRelevantOutputLines(result.Output))
                        {
                            if (loggedOutputLines.Add(line.Trim()))
                            {
                                appendOutput(line);
                            }
                        }
                    }

                    var isNoApplicableUpgrade = _wingetService.IsNoApplicableUpgrade(result);
                    var hasAdvertisedUpdate = HasAdvertisedUpdate(update);
                    var message = isNoApplicableUpgrade
                        ? GetNoApplicableUpgradeMessage(strings.LocaleCode)
                        : hasAdvertisedUpdate
                            ? GetAdvertisedUpdateNoopMessage(strings.LocaleCode)
                        : _wingetService.GetErrorMessage(result.ExitCode, strings.LocaleCode);
                    var resolution = isNoApplicableUpgrade
                        ? GetNoApplicableUpgradeResolution(strings.LocaleCode, update)
                        : hasAdvertisedUpdate
                            ? GetAdvertisedUpdateNoopResolution(strings.LocaleCode, update)
                        : _wingetService.GetResolutionHint(result.ExitCode, strings.LocaleCode);
                    setStatusById(update.Id, isNoApplicableUpgrade || hasAdvertisedUpdate
                        ? UiStatusState.FromRawText(message)
                        : UiStatusState.FromKey(UiStatusKey.AlreadyUpdated));
                    setErrorById?.Invoke(update.Id, message, resolution);
                    appendOutput($"event=winget_upgrade_noop id=\"{FormatLogValue(update.Id)}\" exit_code={result.ExitCode} message=\"{FormatLogValue(message)}\" resolution=\"{FormatLogValue(resolution)}\"");
                }
                else
                {
                    var error = _wingetService.GetErrorMessage(result.ExitCode, strings.LocaleCode);
                    var resolution = _wingetService.GetResolutionHint(result.ExitCode, strings.LocaleCode);
                    setStatusById(update.Id, UiStatusState.FromRawText(error));
                    setErrorById?.Invoke(update.Id, error, resolution);
                    appendOutput(error);
                }

                reportProgress(CalculateOverallPercentage(index + 1, updates.Count), $"{update.Name}: 100%");
            }

            reportProgress(100, strings.UpdatesEndText);
            appendOutput($"=== {strings.UpdatesEndText} ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");
        }
        catch (OperationCanceledException)
        {
            appendOutput("event=updates_cancelled reason=cancellation_requested");
        }
        catch (Exception ex)
        {
            appendOutput($"event=updates_error message=\"{ex.Message}\"");
            throw;
        }
    }

    private async Task RunInstallOrUpgradeAsync(
        AppEntry app,
        int currentIndex,
        int totalCount,
        Action<string, UiStatusState> setStatusById,
        Action<string> appendOutput,
        Action<int, string> reportProgress,
        LocalizedStrings strings,
        Action<string, string, string>? setErrorById = null,
        CancellationToken cancellationToken = default)
    {
        if (app.RequiresAdvancedArgumentsReview)
        {
            setStatusById(app.OperationKey, UiStatusState.FromRawText(strings.AdvancedArgumentsReviewRequiredText));
            setErrorById?.Invoke(
                app.OperationKey,
                strings.AdvancedArgumentsReviewRequiredText,
                strings.AdvancedArgumentsReviewRequiredResolution);
            appendOutput($"event=advanced_arguments_review_required id=\"{FormatLogValue(app.Id)}\"");
            reportProgress(CalculateOverallPercentage(currentIndex + 1, totalCount), $"{app.Name}: 100%");
            return;
        }

        var installArgs = _installCommandBuilder.BuildInstallArguments(app);
        var elevationMode = ElevationDecisionService.Decide(_isCurrentProcessElevated, app.Scope, app.ElevationRequirement);

        appendOutput($"event=install_command_built id=\"{app.Id}\" args=\"{FormatArgumentsForLog(installArgs)}\" elevation_mode={elevationMode} process_elevated={_isCurrentProcessElevated} scope=\"{app.Scope}\"");

        setErrorById?.Invoke(app.OperationKey, string.Empty, string.Empty);
        setStatusById(app.OperationKey, UiStatusState.FromKey(UiStatusKey.InstallInProgress));
        appendOutput($"--- {app.Name} [{app.Id}] : {strings.OperationInstallLabel} ---");

        if (elevationMode == ElevationMode.ElevatedRequired)
        {
            appendOutput($"event=elevated_launch_starting id=\"{app.Id}\"");
        }

        var installResult = await RunInstallCommandAsync(app, installArgs, elevationMode, currentIndex, totalCount, setStatusById, appendOutput, reportProgress, cancellationToken);

        if (installResult.ExitCode == 0)
        {
            setStatusById(app.OperationKey, UiStatusState.FromKey(UiStatusKey.Ok));
            reportProgress(CalculateOverallPercentage(currentIndex + 1, totalCount), $"{app.Name}: 100%");
            return;
        }

        if (_wingetService.IsNoApplicableInstaller(installResult))
        {
            appendOutput($"event=install_no_applicable_installer_preserved_selectors id=\"{app.Id}\"");
            var noApplicableError = _wingetService.GetErrorMessage(installResult.ExitCode, strings.LocaleCode);
            var noApplicableResolution = GetNoApplicableInstallResolution(strings.LocaleCode, app);
            setStatusById(app.OperationKey, UiStatusState.FromRawText(noApplicableError));
            setErrorById?.Invoke(app.OperationKey, noApplicableError, noApplicableResolution);
            reportProgress(CalculateOverallPercentage(currentIndex + 1, totalCount), $"{app.Name}: 100%");
            return;
        }

        if (_wingetService.IsManifestNotFound(installResult) && TryBuildInstallArgumentsWithoutVersion(installArgs, out var latestVersionInstallArgs))
        {
            appendOutput($"event=install_retry_without_version id=\"{app.Id}\" version=\"{FormatLogValue(app.Version)}\"");
            installResult = await RunInstallCommandAsync(app, latestVersionInstallArgs, elevationMode, currentIndex, totalCount, setStatusById, appendOutput, reportProgress, cancellationToken);
            if (installResult.ExitCode == 0)
            {
                setStatusById(app.OperationKey, UiStatusState.FromKey(UiStatusKey.Ok));
                reportProgress(CalculateOverallPercentage(currentIndex + 1, totalCount), $"{app.Name}: 100%");
                return;
            }
        }

        if (_wingetService.IsAlreadyInstalled(installResult))
        {
            setStatusById(app.OperationKey, UiStatusState.FromKey(UiStatusKey.AlreadyInstalled));
            setErrorById?.Invoke(app.OperationKey, string.Empty, string.Empty);
            reportProgress(CalculateOverallPercentage(currentIndex + 1, totalCount), $"{app.Name}: 100%");
            return;
        }

        if (_wingetService.IsNoUpgradeNeeded(installResult.ExitCode))
        {
            setStatusById(app.OperationKey, UiStatusState.FromKey(UiStatusKey.AlreadyUpdated));
            setErrorById?.Invoke(app.OperationKey, string.Empty, string.Empty);
            reportProgress(CalculateOverallPercentage(currentIndex + 1, totalCount), $"{app.Name}: 100%");
            return;
        }

        var installError = _wingetService.GetErrorMessage(installResult.ExitCode, strings.LocaleCode);
        var installResolution = _wingetService.GetResolutionHint(installResult.ExitCode, strings.LocaleCode);
        setStatusById(app.OperationKey, UiStatusState.FromRawText(installError));
        setErrorById?.Invoke(app.OperationKey, installError, installResolution);
        reportProgress(CalculateOverallPercentage(currentIndex + 1, totalCount), $"{app.Name}: 100%");
    }

    private async Task<WingetCommandResult> RunInstallCommandAsync(
        AppEntry app,
        IReadOnlyList<string> installArgs,
        ElevationMode elevationMode,
        int currentIndex,
        int totalCount,
        Action<string, UiStatusState> setStatusById,
        Action<string> appendOutput,
        Action<int, string> reportProgress,
        CancellationToken cancellationToken)
    {
        if (elevationMode == ElevationMode.ElevatedRequired)
        {
            var logPath = app.SupportsLog ? GetInstallLogPath(app) : null;
            setStatusById(app.OperationKey, UiStatusState.FromKey(UiStatusKey.InstallInProgress));
            reportProgress(IndeterminateProgress, $"{app.Name}: elevated installer running");
            var installResult = await Task.Run(() => _elevatedLauncher.Launch(installArgs, logPath, cancellationToken: cancellationToken), cancellationToken);
            appendOutput(installResult.Output);
            return installResult;
        }

        var receivedLiveOutput = false;
        var result = await Task.Run(() => _wingetService.Invoke(installArgs, line =>
        {
            receivedLiveOutput = true;
            HandleProgressLine(line, app.OperationKey, app.Name, UiStatusKey.InstallInProgress, currentIndex, totalCount, setStatusById, reportProgress);
            if (_wingetService.ShouldLogOutputLine(line))
            {
                appendOutput(line.Trim());
            }
        }, cancellationToken), cancellationToken);

        if (!receivedLiveOutput)
        {
            AppendResultOutput(appendOutput, result);
        }

        return result;
    }

    private string GetInstallLogPath(AppEntry app)
    {
        return string.IsNullOrWhiteSpace(app.LogPath)
            ? _wingetService.CreateOperationLogPath("install", app.OperationKey)
            : Environment.ExpandEnvironmentVariables(app.LogPath.Trim());
    }

    private static bool TryBuildInstallArgumentsWithoutVersion(
        IReadOnlyList<string> installArgs,
        out IReadOnlyList<string> retryInstallArgs)
    {
        return TryBuildInstallArgumentsWithoutOptions(
            installArgs,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--version" },
            out retryInstallArgs);
    }

    private static bool TryBuildInstallArgumentsWithoutOptions(
        IReadOnlyList<string> installArgs,
        IReadOnlySet<string> options,
        out IReadOnlyList<string> retryInstallArgs)
    {
        var retryArgs = new List<string>(installArgs.Count);
        var removedOption = false;
        for (var index = 0; index < installArgs.Count; index++)
        {
            var arg = installArgs[index];
            if (options.Contains(arg))
            {
                removedOption = true;
                if (index + 1 < installArgs.Count)
                {
                    index++;
                }

                continue;
            }

            retryArgs.Add(arg);
        }

        retryInstallArgs = retryArgs;
        return removedOption;
    }

    private async Task RunUninstallAsync(
        AppEntry app,
        int currentIndex,
        int totalCount,
        Action<string, UiStatusState> setStatusById,
        Action<string> appendOutput,
        Action<int, string> reportProgress,
        LocalizedStrings strings,
        Action<string, string, string>? setErrorById = null,
        CancellationToken cancellationToken = default)
    {
        setErrorById?.Invoke(app.OperationKey, string.Empty, string.Empty);
        setStatusById(app.OperationKey, UiStatusState.FromKey(UiStatusKey.UninstallInProgress));
        appendOutput($"--- {app.Name} [{app.Id}] : {strings.OperationUninstallLabel} ---");
        var receivedLiveOutput = false;
        var uninstallResult = await Task.Run(() => _wingetService.UninstallApp(app.Id, line =>
        {
            receivedLiveOutput = true;
            HandleProgressLine(line, app.OperationKey, app.Name, UiStatusKey.UninstallInProgress, currentIndex, totalCount, setStatusById, reportProgress);
            if (_wingetService.ShouldLogOutputLine(line))
            {
                appendOutput(line.Trim());
            }
        }, cancellationToken), cancellationToken);

        if (!receivedLiveOutput)
        {
            AppendResultOutput(appendOutput, uninstallResult);
        }

        if (uninstallResult.ExitCode == 0)
        {
            setStatusById(app.OperationKey, UiStatusState.FromKey(UiStatusKey.Ok));
        }
        else
        {
            var uninstallError = _wingetService.GetErrorMessage(uninstallResult.ExitCode, strings.LocaleCode);
            var uninstallResolution = _wingetService.GetResolutionHint(uninstallResult.ExitCode, strings.LocaleCode);
            setStatusById(app.OperationKey, UiStatusState.FromRawText(uninstallError));
            setErrorById?.Invoke(app.OperationKey, uninstallError, uninstallResolution);
        }

        reportProgress(CalculateOverallPercentage(currentIndex + 1, totalCount), $"{app.Name}: 100%");
    }

    private static void AppendResultOutput(Action<string> appendOutput, WingetCommandResult result)
    {
        if (string.IsNullOrWhiteSpace(result.Output))
        {
            return;
        }

        appendOutput(result.Output);
    }

    private static string FormatArgumentsForLog(IReadOnlyList<string> args)
    {
        var formattedArgs = new List<string>(args.Count);
        var redactNextValue = false;

        foreach (var arg in args)
        {
            if (redactNextValue)
            {
                formattedArgs.Add("[redacted]");
                redactNextValue = false;
                continue;
            }

            formattedArgs.Add(FormatArgumentForLog(arg));
            if (IsSensitiveArgumentOption(arg))
            {
                redactNextValue = true;
            }
        }

        return string.Join(" ", formattedArgs);
    }

    private static bool IsSensitiveArgumentOption(string arg)
    {
        return string.Equals(arg, "--custom", StringComparison.OrdinalIgnoreCase)
            || string.Equals(arg, "--override", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatArgumentForLog(string arg)
    {
        return arg.Contains(' ', StringComparison.Ordinal) ? $"\"{arg}\"" : arg;
    }

    private static string FormatLogValue(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string GetNoApplicableUpgradeMessage(string localeCode)
    {
        return UseEnglish(localeCode)
            ? "Upgrade not applicable"
            : "Aggiornamento non applicabile";
    }

    private static string GetNoApplicableUpgradeResolution(string localeCode, UpdateEntry update)
    {
        var configuredOptions = FormatConfiguredUpdateOptions(update);
        if (!string.IsNullOrWhiteSpace(configuredOptions))
        {
            return UseEnglish(localeCode)
                ? $"winget found a newer version in the source, but no installer applies to the configured package options ({configuredOptions}). Edit the package options to a supported installer, or wait for the package maintainer to publish a matching installer."
                : $"winget ha trovato una versione piu recente nella sorgente, ma nessun installer e compatibile con le opzioni configurate nel pacchetto ({configuredOptions}). Modifica le opzioni del pacchetto scegliendo un installer supportato oppure attendi che il manutentore pubblichi un installer compatibile.";
        }

        return UseEnglish(localeCode)
            ? "winget found a newer version in the source, but its manifest does not apply to this system or its requirements."
            : "winget ha trovato una versione piu recente nella sorgente, ma il manifest non si applica a questo sistema o ai suoi requisiti.";
    }

    private static string GetAdvertisedUpdateNoopMessage(string localeCode)
    {
        return UseEnglish(localeCode)
            ? "Advertised update not applied"
            : "Aggiornamento segnalato non applicato";
    }

    private static string GetAdvertisedUpdateNoopResolution(string localeCode, UpdateEntry update)
    {
        var currentVersion = string.IsNullOrWhiteSpace(update.Version) ? "unknown" : update.Version.Trim();
        var availableVersion = string.IsNullOrWhiteSpace(update.Available) ? "unknown" : update.Available.Trim();
        return UseEnglish(localeCode)
            ? $"winget listed {currentVersion} -> {availableVersion}, but upgrade returned already at the latest version. This usually means the installed major version or installer channel cannot be upgraded in place; review the package options or install the newer channel manually."
            : $"winget ha elencato {currentVersion} -> {availableVersion}, ma upgrade ha risposto gia alla versione piu recente. Di solito significa che la major version o il canale installer installato non puo essere aggiornato in-place; verifica le opzioni del pacchetto o installa manualmente il canale piu recente.";
    }

    private static bool HasAdvertisedUpdate(UpdateEntry update)
    {
        return !string.IsNullOrWhiteSpace(update.Available)
            && !IsNoUpdateMarker(update.Available)
            && !string.Equals(update.Version?.Trim(), update.Available.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNoUpdateMarker(string value)
    {
        var normalized = value.Trim();
        return normalized.Equals("No update", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("No update available", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Nessun aggiornamento", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Gia alla versione piu recente", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetNoApplicableInstallResolution(string localeCode, AppEntry app)
    {
        var configuredOptions = FormatConfiguredInstallOptions(app);
        if (!string.IsNullOrWhiteSpace(configuredOptions))
        {
            return UseEnglish(localeCode)
                ? $"winget did not find an installer matching the configured package options ({configuredOptions}). OnlyWinget did not retry without these constraints. Edit the package options to a supported installer, or install the package manually if those constraints are required."
                : $"winget non ha trovato un installer compatibile con le opzioni configurate nel pacchetto ({configuredOptions}). OnlyWinget non ha ritentato senza questi vincoli. Modifica le opzioni del pacchetto scegliendo un installer supportato oppure installa il pacchetto manualmente se quei vincoli sono necessari.";
        }

        return UseEnglish(localeCode)
            ? "winget did not find an installer that applies to this system or its requirements. Edit the package options or install the package manually."
            : "winget non ha trovato un installer applicabile a questo sistema o ai suoi requisiti. Modifica le opzioni del pacchetto oppure installa il pacchetto manualmente.";
    }

    private static string FormatConfiguredUpdateOptions(UpdateEntry update)
    {
        var options = new List<string>();
        AddConfiguredOption(options, "scope", update.Scope);
        AddConfiguredOption(options, "architecture", update.Architecture);
        AddConfiguredOption(options, "locale", update.Locale);
        AddConfiguredOption(options, "installer-type", update.InstallerType);
        return string.Join(", ", options);
    }

    private static string FormatConfiguredInstallOptions(AppEntry app)
    {
        var options = new List<string>();
        AddConfiguredOption(options, "scope", app.Scope);
        AddConfiguredOption(options, "architecture", app.Architecture);
        AddConfiguredOption(options, "locale", app.Locale);
        AddConfiguredOption(options, "installer-type", app.InstallerType);
        return string.Join(", ", options);
    }

    private static void AddConfiguredOption(List<string> options, string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            options.Add($"{name}={value.Trim()}");
        }
    }

    private static bool UseEnglish(string localeCode)
    {
        return !string.IsNullOrWhiteSpace(localeCode)
            && localeCode.StartsWith("en", StringComparison.OrdinalIgnoreCase);
    }

    private void HandleProgressLine(
        string line,
        string packageId,
        string packageName,
        UiStatusKey progressStatusKey,
        int currentIndex,
        int totalCount,
        Action<string, UiStatusState> setStatusById,
        Action<int, string> reportProgress)
    {
        if (!WingetProgressParser.TryParse(line, out var progress))
        {
            return;
        }

        if (progress.Percentage.HasValue)
        {
            var currentPackagePercentage = progress.Percentage.Value;
            setStatusById(packageId, UiStatusState.FromKey(progressStatusKey, currentPackagePercentage));
            reportProgress(
                CalculateOverallPercentage(currentIndex, totalCount, currentPackagePercentage),
                $"{packageName}: {currentPackagePercentage}%");
            return;
        }

        if (progress.IsIndeterminate)
        {
            setStatusById(packageId, UiStatusState.FromKey(progressStatusKey));
            reportProgress(IndeterminateProgress, $"{packageName}: {progress.PhaseText}");
        }
    }

    private static int CalculateOverallPercentage(int completedPackages, int totalPackages)
    {
        if (totalPackages <= 0)
        {
            return 0;
        }

        var overall = (completedPackages * 100.0) / totalPackages;
        return Math.Max(0, Math.Min(100, (int)Math.Round(overall)));
    }

    private static int CalculateOverallPercentage(int completedPackages, int totalPackages, int currentPackagePercentage)
    {
        if (totalPackages <= 0)
        {
            return 0;
        }

        var safeCompleted = Math.Max(0, completedPackages);
        var safeCurrent = Math.Max(0, Math.Min(100, currentPackagePercentage));
        var overall = ((safeCompleted * 100.0) + safeCurrent) / totalPackages;
        return Math.Max(0, Math.Min(100, (int)Math.Round(overall)));
    }
}
