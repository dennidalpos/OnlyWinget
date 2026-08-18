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
                "--disable-interactivity",
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

    [Theory]
    [InlineData("valid-source_1.2")]
    [InlineData("My Source")]
    public void ValidateInputAcceptsSafeCharacters(string value) =>
        WingetCommandBuilder.ValidateInput(value, nameof(value));

    [Theory]
    [InlineData("evil\" & calc")]
    [InlineData("name; rm -rf /")]
    [InlineData("name|pipe")]
    [InlineData("name`backtick`")]
    [InlineData("name'quote'")]
    [InlineData("name<redirect>")]
    public void ValidateInputRejectsShellMetacharacters(string value) =>
        Assert.Throws<ArgumentException>(() => WingetCommandBuilder.ValidateInput(value, nameof(value)));
}
