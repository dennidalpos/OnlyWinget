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
        Assert.Equal("2.54.0", row["Version"]);
        Assert.Equal("winget", row["Source"]);
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

    [Fact]
    public void ParseReadsCompactTablesUsingSeparatorSpaces()
    {
        const string output = """
            Nome    Id          Versione
            ------- ----------- --------
            AppOne  Company.One 1.0.0
            AppTwo  Company.Two 2.0.0
            """;

        var rows = new WingetTableParser().Parse(output);

        Assert.Equal(2, rows.Count);
        Assert.Equal("Company.One", rows[0]["Id"]);
        Assert.Equal("1.0.0", rows[0]["Version"]);
        Assert.Equal("Company.Two", rows[1]["Id"]);
        Assert.Equal("2.0.0", rows[1]["Version"]);
    }
}
