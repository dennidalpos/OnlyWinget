using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Presets;

namespace OnlyWinget.Tests;

public sealed class PresetTests
{
    [Fact]
    public void PresetsWithSameNameAreEqualCaseInsensitively()
    {
        var preset1 = new Preset("my-preset", [new PackageIdentity("a", "b")]);
        var preset2 = new Preset("MY-PRESET", [new PackageIdentity("c", "d")]);

        Assert.Equal(preset1, preset2);
        Assert.Equal(preset1.GetHashCode(), preset2.GetHashCode());
    }

    [Fact]
    public void PresetsWithDifferentNamesAreNotEqual()
    {
        var preset1 = new Preset("preset-1", []);
        var preset2 = new Preset("preset-2", []);

        Assert.NotEqual(preset1, preset2);
    }
}
