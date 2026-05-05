// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OnlyWinget.ViewModels;

namespace OnlyWinget.Services;

public sealed class AppStartupCoordinator
{
    public const string AppInstallerDownloadUrl = "https://apps.microsoft.com/detail/9NBLGGH4NNS1";

    private readonly WingetService _wingetService;
    private readonly IDialogService _dialogService;
    private readonly Action<string> _openExternalUrl;

    public AppStartupCoordinator(
        WingetService wingetService,
        IDialogService dialogService,
        Action<string>? openExternalUrl = null)
    {
        _wingetService = wingetService;
        _dialogService = dialogService;
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
        try
        {
            var strings = viewModel.Strings;
            var sourceUpdate = await Task.Run(_wingetService.UpdateSources).ConfigureAwait(true);
            if (sourceUpdate.ExitCode != 0)
            {
                viewModel.AppendLog(sourceUpdate.Output);
            }

            var versionCheck = await _wingetService.CheckForWingetUpdateAsync().ConfigureAwait(true);

            if (!versionCheck.IsUpdateAvailable)
            {
                return;
            }

            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine(string.Format(strings.WingetUpdateAvailableText, versionCheck.InstalledVersion, versionCheck.LatestVersion));
            promptBuilder.AppendLine();
            promptBuilder.Append(strings.WingetUpdatePromptText);

            if (!_dialogService.Confirm(promptBuilder.ToString(), strings.PrerequisitesWarningTitle))
            {
                return;
            }

            viewModel.SetWingetUpdatingStatus();
            viewModel.IsWingetUpdateInProgress = true;
            try
            {
                var result = await Task.Run(_wingetService.UpgradeWinget).ConfigureAwait(true);
                viewModel.AppendLog(result.Output);
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
                _wingetService.CleanupOldLogs();
                viewModel.IsWingetUpdateInProgress = false;
                viewModel.ClearShellStatus();
            }
        }
        catch
        {
            // Check non bloccante: in caso di errore non deve bloccare l'apertura UI.
        }
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
