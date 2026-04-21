// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

namespace OnlyWinget.Models;

public sealed class PackageInterrogationDialogResult
{
    public PackageInterrogationResult Interrogation { get; init; } = new();
    public SelectedInstallOptions SelectedOptions { get; init; } = new();
    public IReadOnlyList<SelectedInstallOptions> QueueSelections { get; init; } = Array.Empty<SelectedInstallOptions>();
}
