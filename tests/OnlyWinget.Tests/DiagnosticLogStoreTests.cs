using OnlyWinget.Application.System;
using OnlyWinget.Infrastructure.Diagnostics;

namespace OnlyWinget.Tests;

public sealed class DiagnosticLogStoreTests
{
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
