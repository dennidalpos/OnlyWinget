// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System.Collections.Generic;
using OnlyWinget.Models;

namespace OnlyWinget.Services;

public enum AppEntryValidationError
{
    None,
    EmptyId,
    DuplicateId,
    InvalidId
}

public interface IAppEntryService
{
    AppEntryValidationError ValidateForInsert(string? id, IEnumerable<AppEntry> currentApps, string? source = "winget", string? architecture = null);
    AppEntryValidationError ValidateResolvedForInsert(string? id, IEnumerable<AppEntry> currentApps, string? source = "winget", string? architecture = null);
    AppEntryValidationError ValidateForEdit(string? id, string? originalId, IEnumerable<AppEntry> currentApps, string? source = "winget", string? architecture = null, string? originalSource = "winget", string? originalArchitecture = null);
    SavedPackageResolutionResult ResolveSavedPackage(AppEntry app);
    AppEntry Create(string? name, string id, string? source = "winget", string? action = null);
    AppEntry Create(PackageInterrogationResult interrogation, SelectedInstallOptions selectedOptions, string? action = null);
}
