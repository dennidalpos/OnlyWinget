// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

namespace OnlyWinget.Models;

public sealed class OperatingSystemInfo
{
    public string ProductName { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Build { get; init; } = string.Empty;
    public string NormalizedArchitecture { get; init; } = string.Empty;
    public string ProcessArchitecture { get; init; } = string.Empty;
    public string UiCultureName { get; init; } = string.Empty;

    public string DisplayText
    {
        get
        {
            var parts = new[]
            {
                ProductName,
                NormalizedArchitecture,
                UiCultureName
            };

            return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }
    }
}
