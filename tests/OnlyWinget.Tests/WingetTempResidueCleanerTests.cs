using OnlyWinget.Infrastructure.Winget;

namespace OnlyWinget.Tests;

public sealed class WingetTempResidueCleanerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "onlywinget-test-" + Guid.NewGuid().ToString("N"));

    public WingetTempResidueCleanerTests() => Directory.CreateDirectory(root);

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RemovesOnlyFreshFoldersOfTheFailedPackage()
    {
        var failed = Directory.CreateDirectory(Path.Combine(root, "Vendor.App.1.2.3")).FullName;
        var lookalike = Directory.CreateDirectory(Path.Combine(root, "Vendor.AppExtra.9.0")).FullName;
        var other = Directory.CreateDirectory(Path.Combine(root, "Other.Tool.1.0")).FullName;
        var cache = Directory.CreateDirectory(Path.Combine(root, "cache")).FullName;
        var stale = Directory.CreateDirectory(Path.Combine(root, "Vendor.App.0.9.0")).FullName;
        File.WriteAllText(Path.Combine(failed, "installer.exe"), "x");
        Directory.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddHours(-3));

        var cleaner = new WingetTempResidueCleaner(root);
        var removed = await cleaner.CleanAsync(["Vendor.App"], DateTimeOffset.UtcNow.AddMinutes(-1), CancellationToken.None);

        Assert.Equal(["Vendor.App.1.2.3"], removed);
        Assert.False(Directory.Exists(failed));
        Assert.True(Directory.Exists(lookalike));
        Assert.True(Directory.Exists(other));
        Assert.True(Directory.Exists(cache));
        Assert.True(Directory.Exists(stale));
    }

    [Fact]
    public async Task MissingRootIsNotAnError()
    {
        var cleaner = new WingetTempResidueCleaner(Path.Combine(root, "missing"));

        Assert.Empty(await cleaner.CleanAsync(["Vendor.App"], DateTimeOffset.UtcNow, CancellationToken.None));
    }
}
