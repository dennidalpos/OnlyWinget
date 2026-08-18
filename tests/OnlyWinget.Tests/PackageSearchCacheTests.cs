using Microsoft.Extensions.Caching.Memory;
using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Infrastructure.Winget;
using Xunit;

namespace OnlyWinget.Tests;

public class PackageSearchCacheTests
{
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
