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
}
