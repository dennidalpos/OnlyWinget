using OnlyWinget.Application.System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OnlyWinget.Tests;

public sealed class TextResourcesTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("it")]
    public void LiteralUiResourceKeys_ResolveInBothLanguages(string language)
    {
        var root = RepositoryRoot();
        var uiDirectory = Path.Combine(root, "src", "OnlyWinget");
        var keys = Directory.EnumerateFiles(uiDirectory, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) is ".cs" or ".xaml")
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), "TextResources\\.Get\\(\"([^\"\\r\\n]+)\"\\)|Localize Key=([^},]+)")
                .Select(match => match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value))
            .Distinct().ToArray();
        Assert.NotEmpty(keys);
        var originalCulture = TextResources.OverrideCulture;
        try
        {
            TextResources.OverrideCulture = CultureInfo.GetCultureInfo(language);
            foreach (var key in keys)
            {
                var translation = TextResources.Get(key);
                Assert.False(string.IsNullOrWhiteSpace(translation), $"Empty {language} translation: {key}");
                Assert.NotEqual(key, translation);
            }
            foreach (var level in Enum.GetValues<AppLogLevel>())
            {
                var key = $"Logs_Level_{level}";
                Assert.NotEqual(key, TextResources.Get(key));
            }
            Assert.Contains("42", string.Format(TextResources.Get("Logs_Count"), 42));
        }
        finally { TextResources.OverrideCulture = originalCulture; }
    }

    [Fact]
    public void MaintainedDictionaries_HaveMatchingKeysAndValidFormats()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "OnlyWinget", "TextResources.cs"));
        var keys = Regex.Matches(source, "\\[\"([^\"]+)\"\\]\\s*=")
            .Select(match => match.Groups[1].Value).GroupBy(key => key).ToArray();
        Assert.All(keys, group => Assert.Equal(2, group.Count()));
        var originalCulture = TextResources.OverrideCulture;
        try
        {
            foreach (var language in new[] { "en", "it" })
            {
                TextResources.OverrideCulture = CultureInfo.GetCultureInfo(language);
                foreach (var key in keys)
                {
                    var translated = TextResources.Get(key.Key);
                    Assert.NotNull(CompositeFormat.Parse(translated));
                }
            }
        }
        finally { TextResources.OverrideCulture = originalCulture; }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OnlyWinget.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository sources are required for resource verification.");
    }
}
