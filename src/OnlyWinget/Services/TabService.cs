// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using OnlyWinget.Models;

namespace OnlyWinget.Services;

public sealed class TabService : ITabService
{
    public bool TryCreate(string? requestedName, Dictionary<string, ObservableCollection<AppEntry>> tabs, ObservableCollection<string> tabNames, out string createdName, out TabOperationError error)
    {
        createdName = (requestedName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(createdName))
        {
            error = TabOperationError.EmptyName;
            return false;
        }

        if (tabs.ContainsKey(createdName))
        {
            error = TabOperationError.AlreadyExists;
            return false;
        }

        tabs[createdName] = new ObservableCollection<AppEntry>();
        tabNames.Add(createdName);
        error = TabOperationError.None;
        return true;
    }

    public bool TryRename(string? selectedTabName, string? requestedName, Dictionary<string, ObservableCollection<AppEntry>> tabs, ObservableCollection<string> tabNames, out string renamedName, out TabOperationError error)
    {
        renamedName = (requestedName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(selectedTabName) || !tabs.TryGetValue(selectedTabName, out var apps))
        {
            error = TabOperationError.NotFound;
            return false;
        }

        if (string.IsNullOrWhiteSpace(renamedName))
        {
            error = TabOperationError.EmptyName;
            return false;
        }

        if (string.Equals(renamedName, selectedTabName, StringComparison.Ordinal))
        {
            error = TabOperationError.None;
            return false;
        }

        if (tabs.ContainsKey(renamedName))
        {
            error = TabOperationError.AlreadyExists;
            return false;
        }

        tabs.Remove(selectedTabName);
        tabs[renamedName] = apps;

        var index = tabNames.IndexOf(selectedTabName);
        if (index >= 0)
        {
            tabNames[index] = renamedName;
        }

        error = TabOperationError.None;
        return true;
    }

    public bool TryDelete(string? selectedTabName, Dictionary<string, ObservableCollection<AppEntry>> tabs, ObservableCollection<string> tabNames, out string nextSelectedName, out TabOperationError error)
    {
        nextSelectedName = string.Empty;
        if (tabs.Count <= 1 || tabNames.Count <= 1)
        {
            error = TabOperationError.CannotDeleteLast;
            return false;
        }

        if (string.IsNullOrWhiteSpace(selectedTabName) || !tabs.ContainsKey(selectedTabName))
        {
            error = TabOperationError.NotFound;
            return false;
        }

        var oldIndex = tabNames.IndexOf(selectedTabName);
        tabs.Remove(selectedTabName);
        tabNames.Remove(selectedTabName);

        if (tabNames.Count == 0)
        {
            error = TabOperationError.NotFound;
            return false;
        }

        if (oldIndex >= tabNames.Count)
        {
            oldIndex = tabNames.Count - 1;
        }

        nextSelectedName = tabNames[Math.Max(oldIndex, 0)];
        error = TabOperationError.None;
        return true;
    }
}
