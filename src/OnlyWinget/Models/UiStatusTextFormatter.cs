// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using OnlyWinget.Services;

namespace OnlyWinget.Models;

internal static class UiStatusTextFormatter
{
    public static string Format(UiStatusKey key, int? progressPercentage, string rawText, LocalizedStrings strings)
    {
        if (!string.IsNullOrWhiteSpace(rawText))
        {
            return rawText;
        }

        var baseText = key switch
        {
            UiStatusKey.Ok => strings.StatusOk,
            UiStatusKey.Paused => strings.StatusPaused,
            UiStatusKey.UpgradeInProgress => strings.StatusUpgradeInProgress,
            UiStatusKey.AlreadyUpdated => strings.StatusAlreadyUpdated,
            UiStatusKey.InstallInProgress => strings.StatusInstallInProgress,
            UiStatusKey.AlreadyInstalled => strings.StatusAlreadyInstalled,
            UiStatusKey.UninstallInProgress => strings.StatusUninstallInProgress,
            _ => string.Empty
        };

        return progressPercentage.HasValue && !string.IsNullOrWhiteSpace(baseText)
            ? $"{baseText} {progressPercentage.Value}%"
            : baseText;
    }
}
