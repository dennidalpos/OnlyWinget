// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

namespace OnlyWinget.Models;

public sealed class UiStatusState
{
    public UiStatusKey Key { get; init; }

    public int? ProgressPercentage { get; init; }

    public string RawText { get; init; } = string.Empty;

    public static UiStatusState None() => new();

    public static UiStatusState FromKey(UiStatusKey key, int? progressPercentage = null)
    {
        return new UiStatusState
        {
            Key = key,
            ProgressPercentage = progressPercentage
        };
    }

    public static UiStatusState FromRawText(string rawText)
    {
        return new UiStatusState
        {
            Key = UiStatusKey.None,
            RawText = rawText ?? string.Empty
        };
    }
}
