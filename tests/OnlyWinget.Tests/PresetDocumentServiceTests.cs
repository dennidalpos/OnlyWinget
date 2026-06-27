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
}
