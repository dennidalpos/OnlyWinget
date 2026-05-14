// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using OnlyWinget.Models;

namespace OnlyWinget.Services;

internal static class UpdateVerificationFormatter
{
    public static string FormatStillAvailableStatus(string localeCode)
    {
        return UseEnglish(localeCode)
            ? "Update still available"
            : "Aggiornamento ancora disponibile";
    }

    public static string FormatStillAvailableResolution(string localeCode, UpdateEntry attemptedUpdate, UpdateEntry refreshedUpdate)
    {
        var currentVersion = string.IsNullOrWhiteSpace(refreshedUpdate.Version)
            ? attemptedUpdate.Version
            : refreshedUpdate.Version;
        var availableVersion = string.IsNullOrWhiteSpace(refreshedUpdate.Available)
            ? attemptedUpdate.Available
            : refreshedUpdate.Available;

        return UseEnglish(localeCode)
            ? $"winget still reports {currentVersion} -> {availableVersion} after the update attempt. The installer exited without changing the registered installed version. The row was deselected to avoid repeating the same installer; open the operation log folder if a package log was created."
            : $"winget segnala ancora {currentVersion} -> {availableVersion} dopo il tentativo di aggiornamento. Il programma di installazione e terminato senza cambiare la versione installata registrata. La riga e stata deselezionata per evitare di ripetere lo stesso installer; apri la cartella log se e stato creato un log del pacchetto.";
    }

    public static string FormatStillAvailableLog(UpdateEntry attemptedUpdate, UpdateEntry refreshedUpdate)
    {
        var currentVersion = string.IsNullOrWhiteSpace(refreshedUpdate.Version)
            ? attemptedUpdate.Version
            : refreshedUpdate.Version;
        var availableVersion = string.IsNullOrWhiteSpace(refreshedUpdate.Available)
            ? attemptedUpdate.Available
            : refreshedUpdate.Available;
        var source = string.IsNullOrWhiteSpace(refreshedUpdate.Source)
            ? attemptedUpdate.Source
            : refreshedUpdate.Source;

        return $"event=update_still_available id=\"{EscapeLogValue(refreshedUpdate.Id)}\" name=\"{EscapeLogValue(refreshedUpdate.Name)}\" version=\"{EscapeLogValue(currentVersion)}\" available=\"{EscapeLogValue(availableVersion)}\" source=\"{EscapeLogValue(source)}\"";
    }

    private static bool UseEnglish(string localeCode)
    {
        return !string.IsNullOrWhiteSpace(localeCode)
            && localeCode.StartsWith("en", StringComparison.OrdinalIgnoreCase);
    }

    private static string EscapeLogValue(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
