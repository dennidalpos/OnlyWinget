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
                var result = await Task.Run(() => _wingetService.UpgradeApp(update.Id, update.Source, line =>
                {
                    receivedLiveOutput = true;
                    HandleProgressLine(line, update.Id, update.Name, UiStatusKey.UpgradeInProgress, index, updates.Count, setStatusById, reportProgress);
                    if (_wingetService.ShouldLogOutputLine(line))
                    {
                        appendOutput(line.Trim());
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
                    setStatusById(update.Id, UiStatusState.FromKey(UiStatusKey.AlreadyUpdated));
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
