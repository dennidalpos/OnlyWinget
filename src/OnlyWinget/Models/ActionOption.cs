// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

namespace OnlyWinget.Models;

public sealed class ActionOption : ObservableObject
{
    private string _label = string.Empty;

    public required string Value { get; init; }

    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }
}
