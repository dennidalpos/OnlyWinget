// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System.Threading;
using System.Threading.Tasks;
using OnlyWinget.Models;

namespace OnlyWinget.Services;

public interface IDialogService
{
    string Prompt(string prompt, string title, string defaultValue = "");
    void ShowInfo(string message, string title);
    void ShowWarning(string message, string title);
    void ShowError(string message, string title);
    bool Confirm(string message, string title);
    string? OpenFile(string title, string filter, string defaultExtension = "json");
    string? SaveFile(string title, string filter, string defaultFileName, string defaultExtension = "json");
    Task<PackageInterrogationDialogResult?> ShowPackageInterrogationAsync(PackageInterrogationRequest request, CancellationToken cancellationToken = default);
    Task<PackageInterrogationDialogResult?> ShowPackageInterrogationEditAsync(PackageInterrogationRequest request, AppEntry existingEntry, CancellationToken cancellationToken = default);
}
