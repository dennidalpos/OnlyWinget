using System.Runtime.Versioning;
using Microsoft.Win32;
using OnlyWinget.Infrastructure.System;

namespace OnlyWinget.Tests;

[SupportedOSPlatform("windows")]
public sealed class LegacyProtocolCleanupTests : IDisposable
{
    private readonly string fixturePath = @"Software\OnlyWingetTests\" + Guid.NewGuid().ToString("N");
    private readonly RegistryKey classes;
    private const string Executable = @"C:\OnlyWinget fixture\OnlyWinget.exe";

    public LegacyProtocolCleanupTests() => classes = Registry.CurrentUser.CreateSubKey(fixturePath);

    [Theory]
    [InlineData("URL:OnlyWinget Protocol", true, true, true)]
    [InlineData("URL:OnlyWinget Protocol", true, false, false)]
    [InlineData("Other handler", true, true, false)]
    [InlineData("URL:OnlyWinget Protocol", false, true, false)]
    public void Cleanup_RemovesOnlyOwnedLegacyRegistration(string label, bool protocolMarker, bool ownCommand, bool removed)
    {
        using (var key = classes.CreateSubKey("onlywinget"))
        {
            key.SetValue(string.Empty, label);
            if (protocolMarker) key.SetValue("URL Protocol", string.Empty);
            using var command = key.CreateSubKey(@"shell\open\command");
            command.SetValue(string.Empty, $"\"{(ownCommand ? Executable.ToUpperInvariant() : @"C:\Other portable\OnlyWinget.exe")}\" \"%1\"");
        }
        using (var unrelated = classes.CreateSubKey("unrelated")) unrelated.SetValue(string.Empty, "preserve");

        Assert.Equal(removed, LegacyProtocolCleanup.RemoveOwnedRegistration(classes, Executable));
        using var remaining = classes.OpenSubKey("onlywinget");
        Assert.Equal(!removed, remaining is not null);
        using var sentinel = classes.OpenSubKey("unrelated");
        Assert.Equal("preserve", sentinel!.GetValue(string.Empty));
    }

    [Fact]
    public void Cleanup_MissingRegistrationIsIdempotent()
    {
        Assert.False(LegacyProtocolCleanup.RemoveOwnedRegistration(classes, Executable));
        Assert.False(LegacyProtocolCleanup.RemoveOwnedRegistration(classes, Executable));
    }

    public void Dispose()
    {
        classes.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(fixturePath, throwOnMissingSubKey: false);
    }
}
