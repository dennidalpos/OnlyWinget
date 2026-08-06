using OnlyWinget.Application.Navigation;

namespace OnlyWinget.Tests;

public sealed class UrlProtocolParserTests
{
    [Theory]
    [InlineData("onlywinget://install?packageId=Git.Git", UrlProtocolAction.Install, "Git.Git")]
    [InlineData("onlywinget://install?id=Microsoft.VisualStudioCode", UrlProtocolAction.Install, "Microsoft.VisualStudioCode")]
    [InlineData("onlywinget://show?packageId=7zip.7zip", UrlProtocolAction.Show, "7zip.7zip")]
    [InlineData("onlywinget://details?id=Google.Chrome", UrlProtocolAction.Show, "Google.Chrome")]
    public void Parse_ValidPackageUrl_ReturnsExpectedActionAndPackageId(string url, UrlProtocolAction expectedAction, string expectedPackageId)
    {
        var request = UrlProtocolParser.Parse(url);

        Assert.True(request.IsValid);
        Assert.Equal(expectedAction, request.Action);
        Assert.Equal(expectedPackageId, request.PackageId);
    }

    [Theory]
    [InlineData("onlywinget://search?q=git", UrlProtocolAction.Search, "git")]
    [InlineData("onlywinget://find?query=visual+studio", UrlProtocolAction.Search, "visual studio")]
    public void Parse_SearchUrl_ReturnsSearchActionAndQuery(string url, UrlProtocolAction expectedAction, string expectedQuery)
    {
        var request = UrlProtocolParser.Parse(url);

        Assert.True(request.IsValid);
        Assert.Equal(expectedAction, request.Action);
        Assert.Equal(expectedQuery, request.Query);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://install?packageId=Git.Git")]
    [InlineData("onlywinget://invalidaction?packageId=Git.Git")]
    [InlineData("onlywinget://install?packageId=")]
    [InlineData("onlywinget://install?packageId=Git.Git;rm-rf")]
    [InlineData("onlywinget://install?packageId=<script>")]
    public void Parse_InvalidOrMaliciousUrl_ReturnsInvalidRequest(string? url)
    {
        var request = UrlProtocolParser.Parse(url);
        Assert.False(request.IsValid);
    }

    [Fact]
    public void SanitizePackageId_RejectsMaliciousInputs()
    {
        Assert.Null(UrlProtocolParser.SanitizePackageId(null));
        Assert.Null(UrlProtocolParser.SanitizePackageId(""));
        Assert.Null(UrlProtocolParser.SanitizePackageId("Git.Git & calc.exe"));
        Assert.Null(UrlProtocolParser.SanitizePackageId("Git.Git; echo hello"));
        Assert.Equal("Git.Git", UrlProtocolParser.SanitizePackageId("Git.Git"));
    }

    [Fact]
    public void SanitizeQuery_StripsDangerousCharacters()
    {
        Assert.Null(UrlProtocolParser.SanitizeQuery(null));
        Assert.Equal("git search", UrlProtocolParser.SanitizeQuery("git search; & |"));
    }
}
