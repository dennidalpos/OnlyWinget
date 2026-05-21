// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Linq;
using OnlyWinget.Services;
using Xunit;

namespace OnlyWinget.Tests;

public sealed class WingetSmokeTests
{
    [SmokeFact]
    [Trait("Category", "Smoke")]
    public void TestAvailable_ReturnsTrue_WhenSmokeModeIsEnabled()
    {
        var service = new WingetService();
        Assert.True(service.TestAvailable());
    }

    [SmokeFact]
    [Trait("Category", "Smoke")]
    public void Search_ReturnsKnownPackage_WhenSmokeModeIsEnabled()
    {
        var service = new WingetService();
        var results = service.Search("Microsoft.PowerToys");

        Assert.Contains(results, result => string.Equals(result.Id, "Microsoft.PowerToys", StringComparison.OrdinalIgnoreCase));
    }
}
