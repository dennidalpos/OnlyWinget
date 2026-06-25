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
    private readonly WingetQueryService _wingetQueryService;

    public AppEntryService(WingetService wingetService)
        : this(new WingetQueryService(wingetService))
    {
    }

    public AppEntryService(WingetQueryService wingetQueryService)
    {
        _wingetQueryService = wingetQueryService ?? throw new ArgumentNullException(nameof(wingetQueryService));
    }

    public AppEntryValidationError ValidateForInsert(string? id, IEnumerable<AppEntry> currentApps, string? source = "winget", string? architecture = null)
    {
        var normalizedId = NormalizeId(id);
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return AppEntryValidationError.EmptyId;
        }

        var normalizedSource = NormalizeSource(source);
        if (ContainsEntry(currentApps, normalizedId, normalizedSource, architecture))
        {
            return AppEntryValidationError.DuplicateId;
        }

        return _wingetQueryService.TestAppExists(normalizedId, normalizedSource)
            ? AppEntryValidationError.None
            : AppEntryValidationError.InvalidId;
    }

    public AppEntryValidationError ValidateResolvedForInsert(string? id, IEnumerable<AppEntry> currentApps, string? source = "winget", string? architecture = null)
    {
        var normalizedId = NormalizeId(id);
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return AppEntryValidationError.EmptyId;
        }

        return ContainsEntry(currentApps, normalizedId, NormalizeSource(source), architecture)
            ? AppEntryValidationError.DuplicateId
            : AppEntryValidationError.None;
    }

    public AppEntryValidationError ValidateForEdit(string? id, string? originalId, IEnumerable<AppEntry> currentApps, string? source = "winget", string? architecture = null, string? originalSource = "winget", string? originalArchitecture = null)
    {
        var normalizedId = NormalizeId(id);
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return AppEntryValidationError.EmptyId;
        }

        var normalizedSource = NormalizeSource(source);
        var normalizedOriginalSource = NormalizeSource(originalSource);
        var normalizedArchitecture = NormalizeArchitecture(architecture);
        var normalizedOriginalArchitecture = NormalizeArchitecture(originalArchitecture);
        var isSameEntry =
            string.Equals(normalizedId, NormalizeId(originalId), StringComparison.OrdinalIgnoreCase)
            && string.Equals(normalizedSource, normalizedOriginalSource, StringComparison.OrdinalIgnoreCase)
            && string.Equals(normalizedArchitecture, normalizedOriginalArchitecture, StringComparison.OrdinalIgnoreCase);

        if (!isSameEntry && ContainsEntry(currentApps, normalizedId, normalizedSource, normalizedArchitecture))
        {
            return AppEntryValidationError.DuplicateId;
        }

        return _wingetQueryService.TestAppExists(normalizedId, normalizedSource)
            ? AppEntryValidationError.None
            : AppEntryValidationError.InvalidId;
    }

    public SavedPackageResolutionResult ResolveSavedPackage(AppEntry app)
    {
        return _wingetQueryService.ResolveSavedPackage(app.Id, app.Name, app.Source);
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
            Source = AppEntry.NormalizeSource(interrogation.Source),
            Action = action ?? AppActions.Install,
            Scope = (selectedOptions.Scope ?? string.Empty).Trim(),
            InstallMode = string.IsNullOrWhiteSpace(selectedOptions.InstallMode) ? InstallModes.SilentWithProgress : selectedOptions.InstallMode.Trim(),
            Architecture = (selectedOptions.Architecture ?? string.Empty).Trim(),
            Locale = (selectedOptions.Locale ?? string.Empty).Trim(),
            InstallerType = (selectedOptions.InstallerType ?? string.Empty).Trim(),
            InstallLocation = (selectedOptions.InstallLocation ?? string.Empty).Trim(),
            LogPath = (selectedOptions.LogPath ?? string.Empty).Trim(),
            SupportsInstallLocation = selectedOptions.SupportsInstallLocation,
            SupportsLog = selectedOptions.SupportsLog,
            AdditionalCustomArgs = (selectedOptions.AdditionalCustomArgs ?? string.Empty).Trim(),
            OverrideArgs = (selectedOptions.OverrideArgs ?? string.Empty).Trim(),
            ElevationRequirement = (selectedOptions.ElevationRequirement ?? string.Empty).Trim(),
            Status = string.Empty
        };
    }

    private static string NormalizeId(string? id) => (id ?? string.Empty).Trim();

    private static string NormalizeArchitecture(string? architecture) => (architecture ?? string.Empty).Trim();

    private static string NormalizeSource(string? source) => AppEntry.NormalizeSource(source);

    private static bool ContainsEntry(IEnumerable<AppEntry> currentApps, string id, string? source, string? architecture)
    {
        var operationKey = AppEntry.BuildOperationKey(id, NormalizeSource(source), architecture);
        return currentApps.Any(app => string.Equals(app.OperationKey, operationKey, StringComparison.OrdinalIgnoreCase));
    }
}
