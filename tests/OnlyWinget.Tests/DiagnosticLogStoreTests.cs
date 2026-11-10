using OnlyWinget.Application.System;
using OnlyWinget.Infrastructure.Diagnostics;

namespace OnlyWinget.Tests;

public sealed class DiagnosticLogStoreTests
{
    [Fact]
    public async Task RollingAndRetentionBoundConcurrentWritesAndPreserveUnrelatedFiles()
    {
        using var fixture = new LogFixture();
        var unrelated = Path.Combine(fixture.Directory, "onlywinget-20261008-user.log");
        File.WriteAllText(unrelated, "preserve");
        var store = new DiagnosticLogStore(fixture.Directory, maxFileBytes: 256, maxFiles: 3);
        await Task.WhenAll(Enumerable.Range(0, 50).Select(index => Task.Run(() => Assert.True(store.Write(Entry($"message-{index:D2}"))))));
        var logs = Directory.GetFiles(fixture.Directory).Where(path => path != unrelated).ToArray();
        Assert.Equal(3, logs.Length);
        Assert.All(logs, path => Assert.InRange(new FileInfo(path).Length, 1, 256));
        Assert.Equal(50, store.GetRecentLogs().Count);
        Assert.Equal("preserve", File.ReadAllText(unrelated));
        Assert.True(store.Clear());
        Assert.Equal(new[] { unrelated }, Directory.GetFiles(fixture.Directory));
        Assert.True(store.Write(Entry("after rolling clear")));
    }

    [Fact]
    public void OversizedEntryRetainsMemoryAndExposesFailureWithoutGrowingDisk()
    {
        using var fixture = new LogFixture();
        var store = new DiagnosticLogStore(fixture.Directory, maxFileBytes: 128, maxFiles: 2);
        Assert.False(store.Write(Entry(new string('x', 256))));
        Assert.Single(store.GetRecentLogs());
        Assert.Contains("disk file limit", store.LastError);
        Assert.Empty(Directory.GetFiles(fixture.Directory));
        Assert.True(store.Write(Entry("fits")));
        Assert.Null(store.LastError);
    }

    [Fact]
    public void LegacyOversizedLogIsPrunedToTheAggregateBudget()
    {
        using var fixture = new LogFixture();
        File.WriteAllText(fixture.LogPath, new string('x', 1024));
        var store = new DiagnosticLogStore(fixture.Directory, maxFileBytes: 128, maxFiles: 2);
        Assert.True(store.Write(Entry("new entry")));
        Assert.False(File.Exists(fixture.LogPath));
        var remaining = Assert.Single(Directory.GetFiles(fixture.Directory));
        Assert.Contains("new entry", File.ReadAllText(remaining));
    }

    [Fact]
    public void RetentionFailureKeepsActiveWriteAndExposesTheError()
    {
        using var fixture = new LogFixture();
        var old = Path.Combine(fixture.Directory, "onlywinget-20261007.log");
        File.WriteAllText(old, "old log");
        using var locked = File.Open(old, FileMode.Open, FileAccess.Read, FileShare.Read);
        var store = new DiagnosticLogStore(fixture.Directory, maxFileBytes: 128, maxFiles: 1);
        Assert.False(store.Write(Entry("active write")));
        Assert.NotNull(store.LastError);
        Assert.Contains("active write", File.ReadAllText(fixture.LogPath));
        Assert.Single(store.GetRecentLogs());
    }
    private static AppLogEntry Entry(string message, AppLogLevel level = AppLogLevel.Information) =>
        new(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero), level, "Test", message);

    [Fact]
    public void DisabledLogging_WritesNeitherFilesNorBufferAndCanBeEnabledLater()
    {
        using var fixture = new LogFixture();
        fixture.Store.Configure(false, AppLogLevel.Verbose);
        Assert.True(fixture.Store.Write(Entry("disabled", AppLogLevel.Error)));
        Assert.Empty(fixture.Store.GetRecentLogs());
        Assert.Empty(Directory.GetFiles(fixture.Directory));
        fixture.Store.Configure(true, AppLogLevel.Verbose);
        Assert.True(fixture.Store.Write(Entry("enabled", AppLogLevel.Verbose)));
        Assert.Equal(AppLogLevel.Verbose, Assert.Single(fixture.Store.GetRecentLogs()).Level);
        Assert.Single(File.ReadAllLines(fixture.LogPath));
    }

    [Fact]
    public void LevelChanges_FilterFileAndBufferConsistently()
    {
        using var fixture = new LogFixture();
        fixture.Store.Configure(true, AppLogLevel.Warning);
        fixture.Store.Write(Entry("hidden"));
        fixture.Store.Write(Entry("warning", AppLogLevel.Warning));
        fixture.Store.Write(Entry("error", AppLogLevel.Error));
        fixture.Store.Configure(true, AppLogLevel.Verbose);
        fixture.Store.Write(Entry("verbose", AppLogLevel.Verbose));
        Assert.Equal(new[] { AppLogLevel.Warning, AppLogLevel.Error, AppLogLevel.Verbose }, fixture.Store.GetRecentLogs().Select(entry => entry.Level));
        Assert.Equal(3, File.ReadAllLines(fixture.LogPath).Length);
        Assert.DoesNotContain("hidden", File.ReadAllText(fixture.LogPath));
    }

    [Fact]
    public async Task ConcurrentWrites_AppearExactlyOnceInFileAndBuffer()
    {
        using var fixture = new LogFixture();
        await Task.WhenAll(Enumerable.Range(0, 100).Select(index => Task.Run(() =>
            Assert.True(fixture.Store.Write(Entry($"message-{index}"))))));
        Assert.Equal(100, fixture.Store.GetRecentLogs().Count);
        Assert.Equal(100, fixture.Store.GetRecentLogs().Select(entry => entry.Message).Distinct().Count());
        var lines = File.ReadAllLines(fixture.LogPath);
        Assert.Equal(100, lines.Length);
        Assert.Equal(100, lines.Distinct().Count());
    }

    [Fact]
    public void ClearWhileRunning_ReleasesFilesPreservesUnrelatedFilesAndAllowsMoreWrites()
    {
        using var fixture = new LogFixture();
        fixture.Store.Write(Entry("before clear"));
        var unrelated = Path.Combine(fixture.Directory, "notes.txt");
        var invalidDate = Path.Combine(fixture.Directory, "onlywinget-99999999.log");
        File.WriteAllText(unrelated, "preserve");
        File.WriteAllText(invalidDate, "preserve");
        Assert.True(fixture.Store.Clear());
        Assert.Empty(fixture.Store.GetRecentLogs());
        Assert.False(File.Exists(fixture.LogPath));
        Assert.Equal("preserve", File.ReadAllText(unrelated));
        Assert.Equal("preserve", File.ReadAllText(invalidDate));
        Assert.True(fixture.Store.Write(Entry("after clear")));
        Assert.Single(File.ReadAllLines(fixture.LogPath));
        Assert.Equal("after clear", Assert.Single(fixture.Store.GetRecentLogs()).Message);
    }

    [Fact]
    public void FailedWrite_PreservesOriginalDiagnosticAndReportsError()
    {
        using var fixture = new LogFixture();
        var blockedDirectory = Path.Combine(fixture.Directory, "file-instead-of-directory");
        File.WriteAllText(blockedDirectory, "preserve");
        var store = new DiagnosticLogStore(blockedDirectory);
        Assert.False(store.Write(Entry("original diagnostic")));
        Assert.NotNull(store.LastError);
        Assert.Equal("original diagnostic", Assert.Single(store.GetRecentLogs()).Message);
        Assert.Equal("preserve", File.ReadAllText(blockedDirectory));
    }

    [Fact]
    public void FailedClear_PreservesBufferAndReportsFailureUntilSuccessfulClear()
    {
        using var fixture = new LogFixture();
        fixture.Store.Write(Entry("preserved"));
        using (File.Open(fixture.LogPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.False(fixture.Store.Clear());
            Assert.NotNull(fixture.Store.LastError);
            Assert.Single(fixture.Store.GetRecentLogs());
        }
        Assert.True(fixture.Store.Clear());
        Assert.Null(fixture.Store.LastError);
        Assert.Empty(fixture.Store.GetRecentLogs());
    }

    [Fact]
    public void BufferIsBoundedAndFiltersUseOriginalLevelCallerAndMessage()
    {
        using var fixture = new LogFixture();
        for (var index = 0; index < 1005; index++) fixture.Store.Write(Entry($"entry-{index}", AppLogLevel.Warning));
        var entries = fixture.Store.GetRecentLogs();
        Assert.Equal(1000, entries.Count);
        Assert.Equal("entry-5", entries[0].Message);
        Assert.Equal("entry-1004", Assert.Single(fixture.Store.GetRecentLogs(AppLogLevel.Warning, "ENTRY-1004")).Message);
        Assert.Empty(fixture.Store.GetRecentLogs(AppLogLevel.Error));
        Assert.Equal(1000, fixture.Store.GetRecentLogs(filterText: "TEST").Count);
        Assert.Equal(1005, File.ReadAllLines(fixture.LogPath).Length);
    }

    private sealed class LogFixture : IDisposable
    {
        public string Directory { get; } = Path.Combine(AppContext.BaseDirectory, "log-fixture-" + Guid.NewGuid().ToString("N"));
        public DiagnosticLogStore Store { get; }
        public string LogPath => Path.Combine(Directory, "onlywinget-20261008.log");

        public LogFixture()
        {
            System.IO.Directory.CreateDirectory(Directory);
            Store = new(Directory);
        }

        public void Dispose()
        {
            foreach (var file in System.IO.Directory.EnumerateFiles(Directory)) File.Delete(file);
            System.IO.Directory.Delete(Directory);
        }
    }
}
