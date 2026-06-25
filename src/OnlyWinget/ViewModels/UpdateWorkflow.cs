// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;
using System.Linq;
using OnlyWinget.Models;
using OnlyWinget.Services;

namespace OnlyWinget.ViewModels;

internal static class UpdateWorkflow
{
    public static void ApplyPresetOptions(IEnumerable<UpdateEntry> updates, IEnumerable<AppEntry> presetApps)
    {
        var configuredApps = presetApps
            .Where(app => !string.Equals(app.Action, AppActions.Pause, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(app.Id))
            .GroupBy(app => app.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var update in updates)
        {
            if (!configuredApps.TryGetValue(update.Id, out var candidates))
            {
                continue;
            }

            var configured = candidates.FirstOrDefault(app => string.Equals(app.Source, update.Source, StringComparison.OrdinalIgnoreCase))
                ?? candidates[0];
            update.Scope = configured.Scope;
            update.Architecture = configured.Architecture;
            update.Locale = configured.Locale;
            update.InstallerType = configured.InstallerType;
        }
    }

    public static void ApplyAttemptResults(
        IEnumerable<UpdateEntry> refreshedUpdates,
        IEnumerable<UpdateEntry> attemptedUpdates,
        IReadOnlyDictionary<string, UiStatusState> finalStatuses,
        IReadOnlyDictionary<string, (string ErrorMessage, string Resolution)> finalErrors,
        LocalizedStrings strings,
        Func<UpdateEntry, string> formatStillAvailableStatus,
        Func<UpdateEntry, UpdateEntry, string> formatStillAvailableResolution,
        Action<string> appendOutput)
    {
        var attemptedById = attemptedUpdates
            .GroupBy(update => update.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var entry in refreshedUpdates)
        {
            if (attemptedById.TryGetValue(entry.Id, out var attemptedUpdate))
            {
                if (finalErrors.TryGetValue(entry.Id, out var attemptedError))
                {
                    entry.Status = attemptedError.ErrorMessage;
                    entry.ErrorMessage = attemptedError.ErrorMessage;
                    entry.Resolution = attemptedError.Resolution;
                    if (string.Equals(
                        attemptedError.ErrorMessage,
                        UpdateVerificationFormatter.FormatStillAvailableStatus(strings.LocaleCode),
                        StringComparison.Ordinal))
                    {
                        entry.IsSelected = false;
                    }

                    continue;
                }

                var stillAvailableStatus = formatStillAvailableStatus(entry);
                entry.Status = stillAvailableStatus;
                entry.ErrorMessage = stillAvailableStatus;
                entry.Resolution = formatStillAvailableResolution(attemptedUpdate, entry);
                entry.IsSelected = false;
                appendOutput(UpdateVerificationFormatter.FormatStillAvailableLog(attemptedUpdate, entry));
                continue;
            }

            if (finalStatuses.TryGetValue(entry.Id, out var status))
            {
                entry.ApplyStatus(status, strings);
            }

            if (finalErrors.TryGetValue(entry.Id, out var error))
            {
                entry.ErrorMessage = error.ErrorMessage;
                entry.Resolution = error.Resolution;
            }
        }
    }
}
