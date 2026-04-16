// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using OnlyWinget.Models;

namespace OnlyWinget.Services;

public enum TabOperationError
{
    None,
    EmptyName,
    AlreadyExists,
    CannotDeleteLast,
    NotFound
}

public interface ITabService
{
    bool TryCreate(string? requestedName, Dictionary<string, ObservableCollection<AppEntry>> tabs, ObservableCollection<string> tabNames, out string createdName, out TabOperationError error);
    bool TryRename(string? selectedTabName, string? requestedName, Dictionary<string, ObservableCollection<AppEntry>> tabs, ObservableCollection<string> tabNames, out string renamedName, out TabOperationError error);
    bool TryDelete(string? selectedTabName, Dictionary<string, ObservableCollection<AppEntry>> tabs, ObservableCollection<string> tabNames, out string nextSelectedName, out TabOperationError error);
}
