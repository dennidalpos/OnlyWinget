using OnlyWinget.Infrastructure.Winget;

namespace OnlyWinget.Tests;

public sealed class WingetTableParserTests
{
    [Fact]
    public void ParseReadsFixedWidthWingetTables()
    {
        const string output = """
            Name               Id                         Version
            -----------------------------------------------------
            PowerToys          Microsoft.PowerToys        1.2.3
            Visual Studio Code Microsoft.VisualStudioCode 4.5.6
            """;

        var rows = new WingetTableParser().Parse(output);

        Assert.Equal(2, rows.Count);
        Assert.Equal("Microsoft.PowerToys", rows[0]["Id"]);
        Assert.Equal("4.5.6", rows[1]["Version"]);
    }

    [Fact]
    public void ParseReadsLocalizedWingetTables()
    {
        const string output = """
            Nome Id      Versione Origine
            ------------------------------
            Git  Git.Git 2.54.0   winget
            """;

        var rows = new WingetTableParser().Parse(output);

        var row = Assert.Single(rows);
        Assert.Equal("Git.Git", row["Id"]);
        Assert.Equal("2.54.0", row["Versione"]);
        Assert.Equal("winget", row["Origine"]);
    }

    [Fact]
    public void ParseIgnoresWingetSpinnerBeforeTable()
    {
        const string output = """
               -
               \
            Name Id      Version
            --------------------
            Git  Git.Git 2.54.0
            """;

        var rows = new WingetTableParser().Parse(output);

        Assert.Equal("Git.Git", Assert.Single(rows)["Id"]);
    }
}
