// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.IO;
using Xunit;

namespace OnlyWinget.Tests;

public sealed class SourceAlignmentTests
{
    [Fact]
    public void MsiSource_AllowsSameVersionUpgrade_ToAvoidDuplicateArpEntriesOnReinstall()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "OnlyWinget.Setup", "OnlyWinget.Setup.wxs"));

        Assert.Contains("<MajorUpgrade", source, StringComparison.Ordinal);
        Assert.Contains("AllowSameVersionUpgrades=\"yes\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TableCellTextStyle_UsesSingleLineEllipsisForCompactRows()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "OnlyWinget", "Styles", "Templates.xaml"));
        var tableCellStyleStart = source.IndexOf("x:Key=\"TableCellTextStyle\"", StringComparison.Ordinal);
        var tableCellStyleEnd = source.IndexOf("</Style>", tableCellStyleStart, StringComparison.Ordinal);

        Assert.True(tableCellStyleStart >= 0);
        Assert.True(tableCellStyleEnd > tableCellStyleStart);

        var styleSource = source[tableCellStyleStart..tableCellStyleEnd];
        Assert.Contains("Property=\"TextWrapping\" Value=\"NoWrap\"", styleSource, StringComparison.Ordinal);
        Assert.Contains("Property=\"TextTrimming\" Value=\"CharacterEllipsis\"", styleSource, StringComparison.Ordinal);
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OnlyWinget.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
