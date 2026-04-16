// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using OnlyWinget.Models;
using OnlyWinget.Services;
using Xunit;

namespace OnlyWinget.Tests;

public sealed class TabServiceTests
{
    [Fact]
    public void TryCreate_AddsNewTab_WhenNameIsValid()
    {
        var service = new TabService();
        var tabs = new Dictionary<string, ObservableCollection<AppEntry>>
        {
            ["Default"] = new()
        };
        var tabNames = new ObservableCollection<string> { "Default" };

        var created = service.TryCreate("Utilities", tabs, tabNames, out var createdName, out var error);

        Assert.True(created);
        Assert.Equal(TabOperationError.None, error);
        Assert.Equal("Utilities", createdName);
        Assert.Contains("Utilities", tabNames);
        Assert.Contains("Utilities", tabs.Keys);
    }

    [Fact]
    public void TryRename_UpdatesDictionaryAndTabNames()
    {
        var service = new TabService();
        var apps = new ObservableCollection<AppEntry> { new() { Id = "Microsoft.PowerToys", Name = "PowerToys" } };
        var tabs = new Dictionary<string, ObservableCollection<AppEntry>>
        {
            ["Default"] = new(),
            ["Utilities"] = apps
        };
        var tabNames = new ObservableCollection<string> { "Default", "Utilities" };

        var renamed = service.TryRename("Utilities", "Tools", tabs, tabNames, out var renamedName, out var error);

        Assert.True(renamed);
        Assert.Equal(TabOperationError.None, error);
        Assert.Equal("Tools", renamedName);
        Assert.DoesNotContain("Utilities", tabs.Keys);
        Assert.Same(apps, tabs["Tools"]);
        Assert.Contains("Tools", tabNames);
    }

    [Fact]
    public void TryDelete_SelectsNextTab_WhenCurrentIsRemoved()
    {
        var service = new TabService();
        var tabs = new Dictionary<string, ObservableCollection<AppEntry>>
        {
            ["Default"] = new(),
            ["Utilities"] = new(),
            ["Games"] = new()
        };
        var tabNames = new ObservableCollection<string> { "Default", "Utilities", "Games" };

        var deleted = service.TryDelete("Utilities", tabs, tabNames, out var nextSelectedName, out var error);

        Assert.True(deleted);
        Assert.Equal(TabOperationError.None, error);
        Assert.Equal("Games", nextSelectedName);
        Assert.DoesNotContain("Utilities", tabNames);
    }

    [Fact]
    public void TryDelete_RefusesToRemoveLastTab()
    {
        var service = new TabService();
        var tabs = new Dictionary<string, ObservableCollection<AppEntry>>
        {
            ["Default"] = new()
        };
        var tabNames = new ObservableCollection<string> { "Default" };

        var deleted = service.TryDelete("Default", tabs, tabNames, out _, out var error);

        Assert.False(deleted);
        Assert.Equal(TabOperationError.CannotDeleteLast, error);
    }
}
