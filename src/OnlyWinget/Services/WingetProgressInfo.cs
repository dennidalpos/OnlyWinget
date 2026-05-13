// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

namespace OnlyWinget.Services;

internal sealed class WingetProgressInfo
{
    public int? Percentage { get; init; }
    public bool IsIndeterminate { get; init; }
    public string PhaseText { get; init; } = string.Empty;
}
