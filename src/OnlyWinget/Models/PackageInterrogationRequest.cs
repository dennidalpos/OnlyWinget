// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;

namespace OnlyWinget.Models;

public sealed class PackageInterrogationRequest
{
    public string PackageId { get; init; } = string.Empty;
    public string PackageName { get; init; } = string.Empty;
    public string Source { get; init; } = AppEntry.DefaultSource;
    public Action<string>? Log { get; init; }
}
