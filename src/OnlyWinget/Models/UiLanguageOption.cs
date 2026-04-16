// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

namespace OnlyWinget.Models;

public sealed class UiLanguageOption
{
    public required string Code { get; init; }

    public required string DisplayName { get; init; }

    public override string ToString()
    {
        return DisplayName;
    }
}
