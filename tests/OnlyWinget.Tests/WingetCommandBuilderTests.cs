using OnlyWinget.Domain.Packages;
using OnlyWinget.Infrastructure.Winget;

namespace OnlyWinget.Tests;

public sealed class WingetCommandBuilderTests
{
    [Fact]
    public void BuildInstallCommandUsesExactIdSourceAndAgreementFlags()
    {
        var builder = new WingetCommandBuilder();
        var selection = new PackageSelection(
            new PackageIdentity("Microsoft.VisualStudioCode", "winget"),
            PackageAction.Install);

        var arguments = builder.Build(selection);

        Assert.Equal(
            [
                "install",
                "--id",
                "Microsoft.VisualStudioCode",
                "--exact",
                "--accept-source-agreements",
                "--accept-package-agreements",
                "--source",
                "winget"
            ],
            arguments);
    }

    [Fact]
    public void BuildUninstallCommandDoesNotAcceptPackageAgreements()
    {
        var builder = new WingetCommandBuilder();
        var selection = new PackageSelection(new PackageIdentity("Git.Git"), PackageAction.Uninstall);

        var arguments = builder.Build(selection);

        Assert.DoesNotContain("--accept-package-agreements", arguments);
    }
}
