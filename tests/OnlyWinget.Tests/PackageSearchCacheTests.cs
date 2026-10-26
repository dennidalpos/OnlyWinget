using Microsoft.Extensions.Caching.Memory;
using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Infrastructure.Winget;
using Xunit;

namespace OnlyWinget.Tests;

public class PackageSearchCacheTests
{
    [Theory]
    [InlineData("Git_My", "Source", "Git", "My_Source")]
    [InlineData("Git_My", null, "Git", "My_")]
    public async Task CollidingDelimitedInputsKeepDistinctResultsAndCacheHits(
        string firstQuery, string? firstSource, string secondQuery, string? secondSource)
    {
        const string output = """
            Name Id      Version
            --------------------
            Git  Git.Git 2.54.0
            """;
        var runner = new DummyCommandRunner(new WingetCommandResult(0, output, string.Empty));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new WingetPackageSearchService(runner, new(), new(), cache);
        var firstRequest = new PackageSearchRequest(firstQuery, firstSource);
        var secondRequest = new PackageSearchRequest(secondQuery, secondSource);

        var first = await service.SearchAsync(firstRequest, CancellationToken.None);
        var second = await service.SearchAsync(secondRequest, CancellationToken.None);

        Assert.Equal(2, runner.CallCount);
        Assert.NotSame(first, second);
        Assert.Equal(firstSource, Assert.Single(first.Rows).Package.Source);
        Assert.Equal(secondSource, Assert.Single(second.Rows).Package.Source);
        Assert.Same(first, await service.SearchAsync(firstRequest, CancellationToken.None));
        Assert.Same(second, await service.SearchAsync(secondRequest, CancellationToken.None));
        Assert.Equal(2, runner.CallCount);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SearchAsync(firstRequest, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SearchAsync(secondRequest, cancellation.Token));
        Assert.Equal(2, runner.CallCount);
    }

    [Fact]
    public async Task CacheDoesNotMixSourcesAndAlreadyCancelledSearchesDoNotRun()
    {
        var runner = new DummyCommandRunner(new WingetCommandResult(0, string.Empty, string.Empty));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new WingetPackageSearchService(runner, new WingetTableParser(), new WingetErrorClassifier(), cache);
        await service.SearchAsync(new("same query", "winget"), CancellationToken.None);
        await service.SearchAsync(new("same query", "msstore"), CancellationToken.None);
        await service.SearchAsync(new("same query", "winget"), CancellationToken.None);
        Assert.Equal(2, runner.CallCount);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SearchAsync(new("same query", "winget"), cancellation.Token));
        Assert.Equal(2, runner.CallCount);
    }

    private class DummyCommandRunner : IWingetCommandRunner
    {
        private readonly WingetCommandResult returnResult;
        public int CallCount { get; private set; }

        public DummyCommandRunner(WingetCommandResult returnResult)
        {
            this.returnResult = returnResult;
        }

        public Task<WingetCommandResult> RunAsync(
            string command,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken,
            IProgress<WingetProgress>? progress = null,
            TimeSpan? timeout = null)
        {
            CallCount++;
            return Task.FromResult(returnResult);
        }
    }

    [Fact]
    public async Task SearchAsync_UsesMemoryCache_OnSubsequentCalls()
    {
        var dummyResult = new WingetCommandResult(0, "Name Id Version\nGit  Git.Git 2.40.0", string.Empty);
        var runner = new DummyCommandRunner(dummyResult);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var searchService = new WingetPackageSearchService(runner, new WingetTableParser(), new WingetErrorClassifier(), memoryCache);

        var request = new PackageSearchRequest("Git");

        var firstCall = await searchService.SearchAsync(request, CancellationToken.None);
        var secondCall = await searchService.SearchAsync(request, CancellationToken.None);

        Assert.Equal(1, runner.CallCount);
        Assert.Same(firstCall, secondCall);
    }
}
