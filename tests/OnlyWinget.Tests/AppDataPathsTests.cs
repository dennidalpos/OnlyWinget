using OnlyWinget.Application.Storage;

namespace OnlyWinget.Tests;

public sealed class AppDataPathsTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("fixture")]
    [InlineData("C:fixture")]
    public void ExplicitInvalidRootCannotFallBackToPersonalData(string root)
    {
        Assert.Throws<ArgumentException>(() => AppDataPaths.ResolveRoot(root, Path.GetTempPath()));
    }

    [Fact]
    public void ExplicitRootDoesNotAppendThePersonalApplicationFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "OnlyWinget-fixture", "profile");
        Assert.Equal(Path.GetFullPath(root), AppDataPaths.ResolveRoot(root, Path.GetTempPath()));
        Assert.Equal(Path.Combine(Path.GetTempPath(), StorageConstants.ApplicationFolderName),
            AppDataPaths.ResolveRoot(null, Path.GetTempPath()));
    }
}
