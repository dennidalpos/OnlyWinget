using OnlyWinget.Application.Presets;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Presets;

namespace OnlyWinget.Tests;

public sealed class PresetDocumentServiceTests
{
    [Fact]
    public void ExportAndImportRoundTripCurrentOnlyWingetFormat()
    {
        var service = new PresetDocumentService();
        var preset = new Preset(
            "Default",
            [new PackageIdentity("Microsoft.PowerToys", "winget")]);

        var json = service.Export(preset);
        var imported = service.Import(json);

        Assert.Equal(preset.Name, imported.Name);
        Assert.Equal(preset.Packages, imported.Packages);
        Assert.Contains(OnlyWingetPresetDocument.CurrentFormat, json, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportRejectsUnsupportedFormats()
    {
        var service = new PresetDocumentService();
        const string unsupportedJson = """
            {
              "format": "onlywinget.unsupported",
              "preset": {
                "name": "Old",
                "packages": []
              }
            }
            """;

        Assert.Throws<NotSupportedException>(() => service.Import(unsupportedJson));
    }

    [Theory]
    [InlineData("{not-json")]
    [InlineData("{\"format\":\"onlywinget.preset.v1\",\"preset\":null}")]
    public void ImportRejectsMalformedDocuments(string json)
    {
        Assert.Throws<InvalidOperationException>(() => new PresetDocumentService().Import(json));
    }

    [Fact]
    public void ImportRejectsDuplicatePackages()
    {
        const string json = """
            {
              "format": "onlywinget.preset.v1",
              "preset": {
                "name": "Duplicates",
                "packages": [
                  { "id": "Git.Git", "source": "winget" },
                  { "id": "git.git", "source": "WINGET" }
                ]
              }
            }
            """;

        Assert.Throws<InvalidOperationException>(() => new PresetDocumentService().Import(json));
    }

    [Theory]
    [InlineData("Work tools", "Work tools.onlywinget.json")]
    [InlineData("Bad:name", "Bad-name.onlywinget.json")]
    public void ExportFileNameUsesSanitizedPresetName(string name, string expected)
    {
        Assert.Equal(expected, PresetDocumentService.GetExportFileName(name));
    }

    [Theory]
    [MemberData(nameof(WingetBoundaryValidationTests.InvalidArguments), MemberType = typeof(WingetBoundaryValidationTests))]
    public void ImportRejectsInvalidPackageIdOrSource(string value)
    {
        var service = new PresetDocumentService();
        foreach (var package in new[] { new PackageIdentity(value, "winget"), new PackageIdentity("Git.Git", value) })
        {
            var json = service.Export(new Preset("Invalid package", [package]));
            Assert.Throws<ArgumentException>(() => service.Import(json));
        }
    }

    [Fact]
    public void ImportRejectsNullPackage()
    {
        const string json = """{"format":"onlywinget.preset.v1","preset":{"name":"Invalid","packages":[null]}}""";
        Assert.Throws<InvalidOperationException>(() => new PresetDocumentService().Import(json));
    }

    [Fact]
    public void ImportPreservesSameIdFromDifferentSourcesAndOptionalSource()
    {
        var service = new PresetDocumentService();
        var preset = new Preset("Sources", [new("Git.Git", "winget"), new("Git.Git", "My Source"), new("Other.Package")]);
        Assert.Equal(preset.Packages, service.Import(service.Export(preset)).Packages);
    }
}
