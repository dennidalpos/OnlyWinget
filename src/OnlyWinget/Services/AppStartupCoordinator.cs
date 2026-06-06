// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OnlyWinget.Models;
using OnlyWinget.ViewModels;

namespace OnlyWinget.Services;

public sealed class AppStartupCoordinator
{
    public const string AppInstallerDownloadUrl = "https://apps.microsoft.com/detail/9NBLGGH4NNS1";

    private readonly WingetService _wingetService;
    private readonly IDialogService _dialogService;
    private readonly PackageOperationService _operationService;
    private readonly Action<string> _openExternalUrl;

    public AppStartupCoordinator(
        WingetService wingetService,
        IDialogService dialogService,
        Action<string>? openExternalUrl = null,
        PackageOperationService? operationService = null)
    {
        _wingetService = wingetService;
        _dialogService = dialogService;
        _operationService = operationService ?? new PackageOperationService(wingetService, new InstallCommandBuilder(wingetService));
        _openExternalUrl = openExternalUrl ?? OpenExternalUrl;
    }

    public bool CanContinueStartup(MainViewModel viewModel)
    {
        if (viewModel.IsWingetAvailable)
        {
            return true;
        }

        var message = $"{viewModel.Strings.WingetNotFoundText}{Environment.NewLine}{Environment.NewLine}{viewModel.Strings.WingetInstallPromptText}";
        if (_dialogService.Confirm(message, viewModel.Strings.WingetNotFoundTitle))
        {
            _openExternalUrl(AppInstallerDownloadUrl);
        }

        return false;
    }

    public async Task RunPostStartupChecksAsync(MainViewModel viewModel)
    {
        var startupCheckStage = "initialization";
        try
        {
            var strings = viewModel.Strings;
            startupCheckStage = "source_update";
            var sourceUpdate = await _operationService.ExecuteAsync(PackageOperationRequest.ForSourceUpdate(), strings).ConfigureAwait(true);
            if (sourceUpdate.ExitCode != 0)
            {
                viewModel.AppendLog(sourceUpdate.Output);
            }

            startupCheckStage = "winget_update_check";
            var versionCheck = await _wingetService.CheckForWingetUpdateAsync().ConfigureAwait(true);

            if (!versionCheck.IsUpdateAvailable)
            {
                return;
            }

            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine(string.Format(strings.WingetUpdateAvailableText, versionCheck.InstalledVersion, versionCheck.LatestVersion));
            promptBuilder.AppendLine();
            promptBuilder.Append(strings.WingetUpdatePromptText);

            startupCheckStage = "winget_update_prompt";
            if (!_dialogService.Confirm(promptBuilder.ToString(), strings.PrerequisitesWarningTitle))
            {
                return;
            }

            viewModel.SetWingetUpdatingStatus();
            viewModel.IsWingetUpdateInProgress = true;
            try
            {
                startupCheckStage = "winget_update_apply";
                var result = await _operationService.ExecuteAsync(PackageOperationRequest.ForUpdateWinget(), strings).ConfigureAwait(true);
                viewModel.AppendLog(result.Output);
                startupCheckStage = "winget_update_verify";
                var installedAfterUpdate = _wingetService.GetInstalledWingetVersion();
                var isUpdatedToExpectedVersion =
                    !string.IsNullOrWhiteSpace(versionCheck.LatestVersion) &&
                    string.Equals(installedAfterUpdate, versionCheck.LatestVersion, StringComparison.OrdinalIgnoreCase);
                var isNowNewerThanBefore =
                    TryParseVersion(installedAfterUpdate, out var installedAfterVersion) &&
                    TryParseVersion(versionCheck.InstalledVersion, out var installedBeforeVersion) &&
                    installedAfterVersion > installedBeforeVersion;

                if (isUpdatedToExpectedVersion || isNowNewerThanBefore || _wingetService.IsNoUpgradeNeeded(result.ExitCode))
                {
                    _dialogService.ShowInfo(strings.WingetUpdateSuccessText, strings.PrerequisitesWarningTitle);
                    return;
                }

                var reason = _wingetService.GetErrorMessage(result.ExitCode);
                _dialogService.ShowWarning(string.Format(strings.WingetUpdateFailedText, reason), strings.PrerequisitesWarningTitle);
            }
            finally
            {
                try
                {
                    startupCheckStage = "cleanup_old_logs";
                    _wingetService.CleanupOldLogs();
                }
                finally
                {
                    viewModel.IsWingetUpdateInProgress = false;
                    viewModel.ClearShellStatus();
                }
            }
        }
        catch (Exception ex)
        {
            viewModel.AppendLog(FormatStartupCheckFailureLog(startupCheckStage, ex));
        }
    }

    private static string FormatStartupCheckFailureLog(string stage, Exception exception)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "event=startup_check_failed stage=\"{0}\" exception_type=\"{1}\" hresult={2}",
            EscapeLogValue(stage),
            EscapeLogValue(exception.GetType().Name),
            exception.HResult);
    }

    private static string EscapeLogValue(string value)
    {
        return (value ?? string.Empty).Replace("\"", "'", StringComparison.Ordinal);
    }

    private static bool TryParseVersion(string raw, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var cleaned = new string(raw.Trim().TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
        if (!Version.TryParse(cleaned, out var parsed) || parsed == null)
        {
            return false;
        }

        version = parsed;
        return true;
    }

    private static void OpenExternalUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // Se l'apertura del browser fallisce, l'app viene comunque chiusa.
        }
    }
}
