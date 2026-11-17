using OnlyWinget.Application.Winget;
using OnlyWinget.Infrastructure.Winget;

namespace OnlyWinget.Tests;

public sealed class WingetExitCodeClassificationTests
{
    private readonly WingetErrorClassifier classifier = new();

    // Codes verified with `winget error <code>`.
    [Theory]
    [InlineData(0x8A150010u, WingetErrorKind.NoApplicableInstaller)]
    [InlineData(0x8A15008Eu, WingetErrorKind.InstallTechnologyMismatch)]
    [InlineData(0x8A15003Au, WingetErrorKind.PolicyBlocked)]
    [InlineData(0x8A150061u, WingetErrorKind.AlreadyInstalled)]
    [InlineData(0x8A150068u, WingetErrorKind.Pinned)]
    [InlineData(0x8A150080u, WingetErrorKind.StoreUnavailable)]
    [InlineData(0x8A150082u, WingetErrorKind.StoreUnavailable)]
    [InlineData(0x8A150101u, WingetErrorKind.InUse)]
    [InlineData(0x8A150102u, WingetErrorKind.InstallInProgress)]
    [InlineData(0x8A150103u, WingetErrorKind.InUse)]
    [InlineData(0x8A150105u, WingetErrorKind.DiskFull)]
    [InlineData(0x8A150107u, WingetErrorKind.Network)]
    [InlineData(0x8A150109u, WingetErrorKind.RebootRequired)]
    [InlineData(0x8A15010Au, WingetErrorKind.RebootRequiredFirst)]
    [InlineData(0x8A15010Bu, WingetErrorKind.RebootRequired)]
    [InlineData(0x8A15010Du, WingetErrorKind.AlreadyInstalled)]
    [InlineData(0x8A15010Eu, WingetErrorKind.Downgrade)]
    [InlineData(0x8A15010Fu, WingetErrorKind.PolicyBlocked)]
    [InlineData(0x8A150111u, WingetErrorKind.InUse)]
    public void NativeExitCodesMapToTheirKind(uint code, WingetErrorKind expected)
    {
        var error = classifier.Classify(new WingetCommandResult(unchecked((int)code), string.Empty, string.Empty));

        Assert.Equal(expected, error?.Kind);
        Assert.Equal($"0x{code:X8}", error?.ExitCodeHex);
    }

    [Theory]
    [InlineData(0x8A150109u, true)]
    [InlineData(0x8A15010Bu, true)]
    [InlineData(0x8A15010Du, true)]
    [InlineData(0x8A15002Bu, true)]
    [InlineData(0x8A15010Au, false)]
    [InlineData(0x8A150068u, false)]
    [InlineData(0x8A150101u, false)]
    public void OnlyRebootPendingAlreadyInstalledAndNoUpdateCountAsSuccess(uint code, bool succeeded)
    {
        var command = new WingetCommandResult(unchecked((int)code), string.Empty, string.Empty);
        var result = new OperationExecutionResult(
            new(new("Pkg.A"), OnlyWinget.Domain.Packages.PackageAction.Upgrade),
            command,
            classifier.Classify(command));

        Assert.Equal(succeeded, result.Succeeded);
    }

    [Theory]
    [InlineData(0x8A150101u)] // in use
    [InlineData(0x8A150068u)] // pinned
    [InlineData(0x8A15010Fu)] // policy
    [InlineData(0x8A150105u)] // disk full
    [InlineData(0x8A150010u)] // no applicable installer
    [InlineData(0x8A150109u)] // reboot pending
    [InlineData(0x8A150123u)] // unmapped native code
    public void DeterministicNativeFailuresAreNotRetried(uint code)
    {
        var command = new WingetCommandResult(unchecked((int)code), string.Empty, string.Empty);

        Assert.False(classifier.IsRetryable(command, classifier.Classify(command)));
    }

    [Theory]
    [InlineData(0x8A150102u)] // another installation in progress
    [InlineData(0x8A150107u)] // no network
    [InlineData(1u)] // generic failure
    public void TransientFailuresAreRetried(uint code)
    {
        var command = new WingetCommandResult(unchecked((int)code), string.Empty, string.Empty);

        Assert.True(classifier.IsRetryable(command, classifier.Classify(command)));
    }

    [Fact]
    public void InstallInProgressWaitsLongerThanOtherRetries()
    {
        var inProgress = new ClassifiedWingetError(WingetErrorKind.InstallInProgress, "busy");
        var other = new ClassifiedWingetError(WingetErrorKind.Network, "offline");

        Assert.True(WingetErrorClassifier.RetryDelayFor(inProgress) > WingetErrorClassifier.RetryDelayFor(other));
    }

    [Fact]
    public void UpgradeCommandCarriesIncludeSwitchesOnlyWhenEnabled()
    {
        var options = new UpdateScanOptions();
        var builder = new WingetCommandBuilder(options);
        var selection = new OnlyWinget.Domain.Packages.PackageSelection(new("Pkg.A", "winget"), OnlyWinget.Domain.Packages.PackageAction.Upgrade);

        Assert.DoesNotContain("--include-unknown", builder.Build(selection));
        Assert.DoesNotContain("--include-pinned", builder.Build(selection));

        options.IncludeUnknownVersions = true;
        options.IncludePinned = true;

        var arguments = builder.Build(selection);
        Assert.Contains("--include-unknown", arguments);
        Assert.Contains("--include-pinned", arguments);

        var install = builder.Build(new OnlyWinget.Domain.Packages.PackageSelection(new("Pkg.A", "winget"), OnlyWinget.Domain.Packages.PackageAction.Install));
        Assert.DoesNotContain("--include-unknown", install);
    }
}
