// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using OnlyWinget.Services;
using Xunit;

namespace OnlyWinget.Tests;

public sealed class WingetTableParserTests
{
    [Fact]
    public void ParseSearchResults_HandlesLocalizedOutputAndProgressNoise()
    {
        var results = WingetTableParser.ParseSearchResults("""
   -
Nome                         ID                         Versione     Origine
---------------------------------------------------------------------------
Microsoft PowerToys          Microsoft.PowerToys        0.90.1       winget
App Installer                Microsoft.AppInstaller     1.28.190     msstore
""");

        Assert.Equal(2, results.Count);
        Assert.Equal("Microsoft PowerToys", results[0].Name);
        Assert.Equal("Microsoft.PowerToys", results[0].Id);
        Assert.Equal("0.90.1", results[0].Version);
        Assert.Equal("winget", results[0].Source);
        Assert.Equal("msstore", results[1].Source);
    }

    [Fact]
    public void ParseUpgradeEntries_IgnoresLocalizedSummaryLine()
    {
        var updates = WingetTableParser.ParseUpgradeEntries("""
Nome                         ID                         Versione     Disponibile  Origine
---------------------------------------------------------------------------------------
Microsoft PowerToys          Microsoft.PowerToys        0.90.0       0.90.1       winget
1 aggiornamenti disponibili.
""");

        var update = Assert.Single(updates);
        Assert.Equal("Microsoft PowerToys", update.Name);
        Assert.Equal("Microsoft.PowerToys", update.Id);
        Assert.Equal("0.90.0", update.Version);
        Assert.Equal("0.90.1", update.Available);
        Assert.Equal("winget", update.Source);
        Assert.True(update.Selected);
    }

    [Fact]
    public void ParseSearchResults_DefaultsSource_WhenOlderWingetOutputOmitsSourceColumn()
    {
        var results = WingetTableParser.ParseSearchResults("""
Name                         Id                         Version
---------------------------------------------------------------
Git                          Git.Git                    2.53.0
""");

        var result = Assert.Single(results);
        Assert.Equal("Git.Git", result.Id);
        Assert.Equal("winget", result.Source);
    }
}
