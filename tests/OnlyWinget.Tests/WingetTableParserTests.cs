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
}
