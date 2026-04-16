// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;
using System.Linq;
using OnlyWinget.Models;

namespace OnlyWinget.Services;

public sealed class AppEntryService : IAppEntryService
{
    private readonly WingetService _wingetService;

    public AppEntryService(WingetService wingetService)
    {
        _wingetService = wingetService;
    }

    public AppEntryValidationError ValidateForInsert(string? id, IEnumerable<AppEntry> currentApps, string? source = "winget")
    {
        var normalizedId = NormalizeId(id);
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return AppEntryValidationError.EmptyId;
        }

        if (ContainsId(currentApps, normalizedId))
        {
            return AppEntryValidationError.DuplicateId;
        }

        return _wingetService.TestAppExists(normalizedId, NormalizeSource(source))
            ? AppEntryValidationError.None
            : AppEntryValidationError.InvalidId;
    }

    public AppEntryValidationError ValidateResolvedForInsert(string? id, IEnumerable<AppEntry> currentApps)
    {
        var normalizedId = NormalizeId(id);
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return AppEntryValidationError.EmptyId;
        }

        return ContainsId(currentApps, normalizedId)
            ? AppEntryValidationError.DuplicateId
            : AppEntryValidationError.None;
    }

    public AppEntryValidationError ValidateForEdit(string? id, string? originalId, IEnumerable<AppEntry> currentApps, string? source = "winget")
    {
        var normalizedId = NormalizeId(id);
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return AppEntryValidationError.EmptyId;
        }

        if (!string.Equals(normalizedId, NormalizeId(originalId), StringComparison.OrdinalIgnoreCase)
            && ContainsId(currentApps, normalizedId))
        {
            return AppEntryValidationError.DuplicateId;
        }

        return _wingetService.TestAppExists(normalizedId, NormalizeSource(source))
            ? AppEntryValidationError.None
            : AppEntryValidationError.InvalidId;
    }

    public AppEntry Create(string? name, string id, string? source = "winget", string? action = null)
    {
        var normalizedId = NormalizeId(id);
        var normalizedName = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            normalizedName = normalizedId;
        }

        return new AppEntry
        {
            Name = normalizedName,
            Id = normalizedId,
            Source = NormalizeSource(source),
            Action = action ?? AppActions.Install,
            Status = string.Empty
        };
    }

    public AppEntry Create(PackageInterrogationResult interrogation, SelectedInstallOptions selectedOptions, string? action = null)
    {
        return new AppEntry
        {
            Name = string.IsNullOrWhiteSpace(interrogation.Name) ? interrogation.Id : interrogation.Name.Trim(),
            Id = NormalizeId(interrogation.Id),
            Source = string.IsNullOrWhiteSpace(interrogation.Source) ? "winget" : interrogation.Source.Trim(),
            Version = (interrogation.Version ?? string.Empty).Trim(),
            Action = action ?? AppActions.Install,
            Scope = (selectedOptions.Scope ?? string.Empty).Trim(),
            InstallMode = string.IsNullOrWhiteSpace(selectedOptions.InstallMode) ? InstallModes.SilentWithProgress : selectedOptions.InstallMode.Trim(),
            Architecture = (selectedOptions.Architecture ?? string.Empty).Trim(),
            Locale = (selectedOptions.Locale ?? string.Empty).Trim(),
            InstallerType = (selectedOptions.InstallerType ?? string.Empty).Trim(),
            InstallLocation = (selectedOptions.InstallLocation ?? string.Empty).Trim(),
            LogPath = (selectedOptions.LogPath ?? string.Empty).Trim(),
            AdditionalCustomArgs = (selectedOptions.AdditionalCustomArgs ?? string.Empty).Trim(),
            OverrideArgs = (selectedOptions.OverrideArgs ?? string.Empty).Trim(),
            ManifestFingerprint = (interrogation.ManifestFingerprint ?? string.Empty).Trim(),
            InterrogatedAtUtc = interrogation.InterrogatedAtUtc.ToString("O"),
            ElevationRequirement = (selectedOptions.ElevationRequirement ?? string.Empty).Trim(),
            Status = string.Empty
        };
    }

    private static string NormalizeId(string? id) => (id ?? string.Empty).Trim();

    private static string NormalizeSource(string? source)
    {
        var value = (source ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(value) ? "winget" : value;
    }

    private static bool ContainsId(IEnumerable<AppEntry> currentApps, string id)
    {
        return currentApps.Any(app => string.Equals(app.Id, id, StringComparison.OrdinalIgnoreCase));
    }
}
