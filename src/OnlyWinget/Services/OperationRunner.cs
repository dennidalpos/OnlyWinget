// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OnlyWinget.Models;

namespace OnlyWinget.Services;

public sealed class OperationRunner : IOperationRunner
{
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
        Action<string, string, string>? setErrorById = null)
    {
        _wingetService.CleanupOldLogs();
        try
        {
            appendOutput($"=== {strings.OperationStartText} ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");
            reportProgress(0, strings.OperationStartText);

            for (var index = 0; index < apps.Count; index++)
            {
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
                        await RunUninstallAsync(app, index, apps.Count, setStatusById, appendOutput, reportProgress, strings, setErrorById);
                        break;

                    default:
                        await RunInstallOrUpgradeAsync(app, index, apps.Count, setStatusById, appendOutput, reportProgress, strings, setErrorById);
                        break;
                }
            }

            reportProgress(100, strings.OperationEndText);
            appendOutput($"=== {strings.OperationEndText} ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");
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
        Action<string, string, string>? setErrorById = null)
    {
        _wingetService.CleanupOldLogs();
        try
        {
            appendOutput($"=== {strings.UpdatesStartText} ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===");
            reportProgress(0, strings.UpdatesStartText);

            for (var index = 0; index < updates.Count; index++)
            {
                var update = updates[index];
                setStatusById(update.Id, UiStatusState.FromKey(UiStatusKey.UpgradeInProgress));
                setErrorById?.Invoke(update.Id, string.Empty, string.Empty);
                appendOutput($"--- {update.Name} [{update.Id}] : {strings.OperationUpgradeLabel} ---");
                var receivedLiveOutput = false;
                var loggedOutputLines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var result = await Task.Run(() => _wingetService.UpgradeApp(
                    update.Id,
                    update.Source,
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
                }));

                if (!receivedLiveOutput)
                {
                    AppendResultOutput(appendOutput, result);
                }

                if (result.ExitCode == 0)
                {
                    setStatusById(update.Id, UiStatusState.FromKey(UiStatusKey.Ok));
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
                    var message = isNoApplicableUpgrade
                        ? GetNoApplicableUpgradeMessage(strings.LocaleCode)
                        : _wingetService.GetErrorMessage(result.ExitCode, strings.LocaleCode);
                    var resolution = isNoApplicableUpgrade
                        ? GetNoApplicableUpgradeResolution(strings.LocaleCode, update)
                        : _wingetService.GetResolutionHint(result.ExitCode, strings.LocaleCode);
                    setStatusById(update.Id, isNoApplicableUpgrade
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
        Action<string, string, string>? setErrorById = null)
    {
        var installArgs = _installCommandBuilder.BuildInstallArguments(app);
        var elevationMode = ElevationDecisionService.Decide(_isCurrentProcessElevated, app.Scope, app.ElevationRequirement);

        appendOutput($"event=install_command_built id=\"{app.Id}\" args=\"{FormatArgumentsForLog(installArgs)}\" elevation_mode={elevationMode} process_elevated={_isCurrentProcessElevated} scope=\"{app.Scope}\"");

        setErrorById?.Invoke(app.OperationKey, string.Empty, string.Empty);
        setStatusById(app.OperationKey, UiStatusState.FromKey(UiStatusKey.InstallInProgress));
        appendOutput($"--- {app.Name} [{app.Id}] : {strings.OperationInstallLabel} ---");

        WingetCommandResult installResult;
        if (elevationMode == ElevationMode.ElevatedRequired)
        {
            appendOutput($"event=elevated_launch_starting id=\"{app.Id}\"");
            var logPath = string.IsNullOrWhiteSpace(app.LogPath)
                ? _wingetService.CreateOperationLogPath("install", app.OperationKey)
                : app.LogPath;
            installResult = await Task.Run(() => _elevatedLauncher.Launch(installArgs, logPath));
            appendOutput(installResult.Output);
        }
        else
        {
            var receivedLiveOutput = false;
            installResult = await Task.Run(() => _wingetService.Invoke(installArgs, line =>
            {
                receivedLiveOutput = true;
                HandleProgressLine(line, app.OperationKey, app.Name, UiStatusKey.InstallInProgress, currentIndex, totalCount, setStatusById, reportProgress);
                if (_wingetService.ShouldLogOutputLine(line))
                {
                    appendOutput(line.Trim());
                }
            }));

            if (!receivedLiveOutput)
            {
                AppendResultOutput(appendOutput, installResult);
            }
        }

        if (installResult.ExitCode == 0)
        {
            setStatusById(app.OperationKey, UiStatusState.FromKey(UiStatusKey.Ok));
            reportProgress(CalculateOverallPercentage(currentIndex + 1, totalCount), $"{app.Name}: 100%");
            return;
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

    private async Task RunUninstallAsync(
        AppEntry app,
        int currentIndex,
        int totalCount,
        Action<string, UiStatusState> setStatusById,
        Action<string> appendOutput,
        Action<int, string> reportProgress,
        LocalizedStrings strings,
        Action<string, string, string>? setErrorById = null)
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
        }));

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

    private static string FormatConfiguredUpdateOptions(UpdateEntry update)
    {
        var options = new List<string>();
        AddConfiguredOption(options, "scope", update.Scope);
        AddConfiguredOption(options, "architecture", update.Architecture);
        AddConfiguredOption(options, "locale", update.Locale);
        AddConfiguredOption(options, "installer-type", update.InstallerType);
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
        if (!_wingetService.TryGetProgressPercentage(line, out var currentPackagePercentage))
        {
            return;
        }

        setStatusById(packageId, UiStatusState.FromKey(progressStatusKey, currentPackagePercentage));
        reportProgress(
            CalculateOverallPercentage(currentIndex, totalCount, currentPackagePercentage),
            $"{packageName}: {currentPackagePercentage}%");
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
