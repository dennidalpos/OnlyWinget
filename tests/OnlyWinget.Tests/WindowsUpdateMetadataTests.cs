using OnlyWinget.Application.Presentation;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Infrastructure.WindowsUpdate;
using System.Runtime.Versioning;

namespace OnlyWinget.Tests;

public sealed class WindowsUpdateMetadataTests
{
    [Theory]
    [InlineData("Critical", false)]
    [InlineData("Moderate", true)]
    [InlineData("Low", false)]
    [InlineData("", true)]
    [InlineData(null, false)]
    [SupportedOSPlatform("windows")]
    public void NativeMetadata_UsesActualSeverityAndCurrentRebootState(string? severity, bool rebootRequired)
    {
        var native = new NativeUpdate { MsrcSeverity = severity, RebootRequired = rebootRequired };
        var item = ComWindowsUpdateService.MapNativeUpdate(native);
        Assert.Equal(new WindowsUpdateIdentity("update-id", 7), item.Identity);
        Assert.Equal(string.IsNullOrEmpty(severity) ? null : severity, item.Severity);
        Assert.Equal(rebootRequired, item.RebootRequired);
        Assert.Equal(new[] { "123456", "KB987654" }, item.KnowledgeBaseArticles);
        Assert.Equal(new[] { "Security Updates" }, item.Categories);
        Assert.Equal(4096UL, item.MaxDownloadSize);
        Assert.True(item.IsDownloaded);

        var state = OnlyWingetApplicationTests.CreateDefaultApplication().State with { WindowsUpdates = [item] };
        var row = Assert.Single(PresentationStateMapper.ToWindowsUpdateState(state).Updates);
        Assert.Equal(item.Severity, row.Severity);
        Assert.Equal(rebootRequired, row.RebootRequired);
        Assert.Equal("KB123456, KB987654", row.KnowledgeBaseArticles);
    }

    [Fact]
    public void KnowledgeBaseFormatting_AddsOnePrefixAndRemovesBlankAndDuplicateEntries()
    {
        var item = new WindowsUpdateItem(new("id", 1), "Update", null, null, [],
            ["123456", " KB987654 ", "kb987654", " ", ""], 0, false, false);
        var state = OnlyWingetApplicationTests.CreateDefaultApplication().State with { WindowsUpdates = [item] };
        var row = Assert.Single(PresentationStateMapper.ToWindowsUpdateState(state).Updates);
        Assert.Equal("KB123456, KB987654", row.KnowledgeBaseArticles);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void NativeMetadata_PropagatesUnreadablePropertiesInsteadOfInventingState()
    {
        Assert.Throws<IOException>(() => ComWindowsUpdateService.MapNativeUpdate(new UnreadableUpdate()));
    }

    public class NativeUpdate
    {
        public NativeIdentity Identity { get; } = new("update-id", 7);
        public string Title => "Security update";
        public string? Description => null;
        public string? MsrcSeverity { get; init; }
        public NativeCollection<NativeCategory> Categories { get; } = new([new("Security Updates")]);
        public NativeCollection<string> KBArticleIDs { get; } = new(["123456", "KB987654"]);
        public ulong MaxDownloadSize => 4096;
        public bool IsDownloaded => true;
        public virtual bool RebootRequired { get; init; }
        public object InstallationBehavior => throw new InvalidOperationException("Potential installation behavior must not be read as current state.");
    }

    public sealed class UnreadableUpdate : NativeUpdate
    {
        public override bool RebootRequired { get => throw new IOException("Native metadata is unreadable."); init { } }
    }

    public sealed record NativeIdentity(string UpdateID, int RevisionNumber);
    public sealed record NativeCategory(string Name);
    public sealed class NativeCollection<T>(T[] items)
    {
        public int Count => items.Length;
        public T Item(int index) => items[index];
    }
}
