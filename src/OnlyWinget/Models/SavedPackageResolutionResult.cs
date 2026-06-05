// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

namespace OnlyWinget.Models;

public enum SavedPackageResolutionStatus
{
    Resolved,
    Unresolved,
    Ambiguous
}

public sealed class SavedPackageResolutionResult
{
    public SavedPackageResolutionStatus Status { get; init; }
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Source { get; init; } = AppEntry.DefaultSource;

    public bool IsResolved => Status == SavedPackageResolutionStatus.Resolved;

    public static SavedPackageResolutionResult Resolved(string id, string name, string source)
    {
        return new SavedPackageResolutionResult
        {
            Status = SavedPackageResolutionStatus.Resolved,
            Id = id,
            Name = name,
            Source = AppEntry.NormalizeSource(source)
        };
    }

    public static SavedPackageResolutionResult Unresolved(string id, string name, string source)
    {
        return new SavedPackageResolutionResult
        {
            Status = SavedPackageResolutionStatus.Unresolved,
            Id = id,
            Name = name,
            Source = AppEntry.NormalizeSource(source)
        };
    }

    public static SavedPackageResolutionResult Ambiguous(string id, string name, string source)
    {
        return new SavedPackageResolutionResult
        {
            Status = SavedPackageResolutionStatus.Ambiguous,
            Id = id,
            Name = name,
            Source = AppEntry.NormalizeSource(source)
        };
    }
}
