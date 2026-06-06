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
    private readonly PackageOperationService _operationService;

    public OperationRunner(
        WingetService wingetService,
        IInstallCommandBuilder installCommandBuilder,
        IElevatedWingetLauncher? elevatedLauncher = null,
        bool? isCurrentProcessElevated = null,
        PackageOperationService? operationService = null)
    {
        _wingetService = wingetService;
        _operationService = operationService ?? new PackageOperationService(
            wingetService,
            installCommandBuilder,
            elevatedLauncher,
            isCurrentProcessElevated);
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
                if (string.IsNullOrWhiteSpace(app.Id))
                {
                    continue;
                }

                if (string.Equals(app.Action, AppActions.Pause, StringComparison.Ordinal))
                {
                    setStatusById(app.OperationKey, UiStatusState.FromKey(UiStatusKey.Paused));
                    setErrorById?.Invoke(app.OperationKey, string.Empty, string.Empty);
                    reportProgress(CalculateOverallPercentage(index + 1, apps.Count), $"{app.Name}: 100%");
                    continue;
                }

                var request = PackageOperationRequest.FromAppEntry(app);
                var progressStatus = request.Kind == PackageOperationKind.Uninstall
                    ? UiStatusKey.UninstallInProgress
                    : UiStatusKey.InstallInProgress;
                var operationLabel = request.Kind == PackageOperationKind.Uninstall
                    ? strings.OperationUninstallLabel
                    : strings.OperationInstallLabel;

                setErrorById?.Invoke(request.OperationKey, string.Empty, string.Empty);
                setStatusById(request.OperationKey, UiStatusState.FromKey(progressStatus));
                appendOutput($"--- {request.Name} [{request.Id}] : {operationLabel} ---");

                var result = await _operationService.ExecuteAsync(
                    request,
                    strings,
                    line =>
                    {
                        HandleProgressLine(line, request.OperationKey, request.Name, progressStatus, index, apps.Count, setStatusById, reportProgress);
                        if (_wingetService.ShouldLogOutputLine(line))
                        {
                            appendOutput(line.Trim());
                        }
                    },
                    cancellationToken,
                    mode =>
                    {
                        if (mode == PackageOperationExecutionMode.Elevated)
                        {
                            reportProgress(IndeterminateProgress, $"{request.Name}: elevated installer running");
                        }
                    }).ConfigureAwait(false);

                ApplyAppOperationResult(result, setStatusById, appendOutput, strings, setErrorById);
                reportProgress(CalculateOverallPercentage(index + 1, apps.Count), $"{request.Name}: 100%");
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
            appendOutput($"event=apply_error message=\"{FormatLogValue(ex.Message)}\"");
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
                var request = PackageOperationRequest.FromUpdateEntry(update);

                setStatusById(update.Id, UiStatusState.FromKey(UiStatusKey.UpgradeInProgress));
                setErrorById?.Invoke(update.Id, string.Empty, string.Empty);
                appendOutput($"--- {update.Name} [{update.Id}] : {strings.OperationUpgradeLabel} ---");

                var result = await _operationService.ExecuteAsync(
                    request,
                    strings,
                    line =>
                    {
                        HandleProgressLine(line, update.Id, update.Name, UiStatusKey.UpgradeInProgress, index, updates.Count, setStatusById, reportProgress);
                        if (_wingetService.ShouldLogOutputLine(line))
                        {
                            appendOutput(line.Trim());
                        }
                    },
                    cancellationToken).ConfigureAwait(false);

                ApplyUpdateOperationResult(result, setStatusById, appendOutput, strings, setErrorById);
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
            appendOutput($"event=updates_error message=\"{FormatLogValue(ex.Message)}\"");
            throw;
        }
    }

    private static void ApplyAppOperationResult(
        PackageOperationResult result,
        Action<string, UiStatusState> setStatusById,
        Action<string> appendOutput,
        LocalizedStrings strings,
        Action<string, string, string>? setErrorById)
    {
        AppendDiagnosticsAndOutput(result, appendOutput);

        switch (result.Outcome)
        {
            case PackageOperationOutcome.Succeeded:
                setStatusById(result.OperationKey, UiStatusState.FromKey(UiStatusKey.Ok));
                setErrorById?.Invoke(result.OperationKey, string.Empty, string.Empty);
                break;

            case PackageOperationOutcome.AlreadyInstalled:
                setStatusById(result.OperationKey, UiStatusState.FromKey(UiStatusKey.AlreadyInstalled));
                setErrorById?.Invoke(result.OperationKey, string.Empty, string.Empty);
                break;

            case PackageOperationOutcome.AlreadyUpdated:
                setStatusById(result.OperationKey, UiStatusState.FromKey(UiStatusKey.AlreadyUpdated));
                setErrorById?.Invoke(result.OperationKey, string.Empty, string.Empty);
                break;

            case PackageOperationOutcome.NoApplicableInstaller:
            case PackageOperationOutcome.AdvancedArgumentsReviewRequired:
            case PackageOperationOutcome.PackageAmbiguous:
            case PackageOperationOutcome.PackageUnresolved:
            case PackageOperationOutcome.Failed:
            default:
                var message = string.IsNullOrWhiteSpace(result.Message)
                    ? strings.ApplyFailedText
                    : result.Message;
                setStatusById(result.OperationKey, UiStatusState.FromRawText(message));
                setErrorById?.Invoke(result.OperationKey, message, result.Resolution);
                break;
        }
    }

    private static void ApplyUpdateOperationResult(
        PackageOperationResult result,
        Action<string, UiStatusState> setStatusById,
        Action<string> appendOutput,
        LocalizedStrings strings,
        Action<string, string, string>? setErrorById)
    {
        AppendDiagnosticsAndOutput(result, appendOutput);

        switch (result.Outcome)
        {
            case PackageOperationOutcome.Succeeded:
                setStatusById(result.OperationKey, UiStatusState.FromKey(UiStatusKey.Ok));
                setErrorById?.Invoke(result.OperationKey, string.Empty, string.Empty);
                break;

            case PackageOperationOutcome.AlreadyUpdated:
                setStatusById(result.OperationKey, UiStatusState.FromKey(UiStatusKey.AlreadyUpdated));
                setErrorById?.Invoke(result.OperationKey, result.Message, result.Resolution);
                break;

            case PackageOperationOutcome.StillAvailable:
            case PackageOperationOutcome.NoApplicableUpgrade:
            case PackageOperationOutcome.AdvertisedUpdateNotApplied:
                setStatusById(result.OperationKey, UiStatusState.FromRawText(result.Message));
                setErrorById?.Invoke(result.OperationKey, result.Message, result.Resolution);
                break;

            case PackageOperationOutcome.Failed:
            default:
                var message = string.IsNullOrWhiteSpace(result.Message)
                    ? strings.UpdatesFailedText
                    : result.Message;
                setStatusById(result.OperationKey, UiStatusState.FromRawText(message));
                setErrorById?.Invoke(result.OperationKey, message, result.Resolution);
                if (!string.IsNullOrWhiteSpace(message))
                {
                    appendOutput(message);
                }
                break;
        }
    }

    private static void AppendDiagnosticsAndOutput(PackageOperationResult result, Action<string> appendOutput)
    {
        foreach (var diagnostic in result.DiagnosticEvents)
        {
            if (!string.IsNullOrWhiteSpace(diagnostic))
            {
                appendOutput(diagnostic);
            }
        }

        if (result.AppendOutput && !string.IsNullOrWhiteSpace(result.Output))
        {
            appendOutput(result.Output);
        }
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

    private static string FormatLogValue(string value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
