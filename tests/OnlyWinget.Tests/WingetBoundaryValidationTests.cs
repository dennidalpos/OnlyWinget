using Microsoft.Extensions.Caching.Memory;
using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Infrastructure.Winget;

namespace OnlyWinget.Tests;

public sealed class WingetBoundaryValidationTests
{
    public static TheoryData<string> InvalidArguments => new()
    {
        "bad\0value", "bad\nvalue", "bad\rvalue", "bad\tvalue", "bad\u007fvalue",
        "bad\"value", "bad'value", "bad`value", "bad;value", "bad|value",
        "bad&value", "bad<value", "bad>value", "--help", "-q", "  --source"
    };

    [Theory]
    [MemberData(nameof(InvalidArguments))]
    public async Task SearchRejectsInvalidQueryOrSourceBeforeCacheOrCli(string value)
    {
        var runner = new RecordingRunner();
        using var cache = new RejectAccessCache();
        var service = new WingetPackageSearchService(runner, new(), new(), cache);
        foreach (var request in new[] { new PackageSearchRequest(value, "winget"), new PackageSearchRequest("Git", value) })
        {
            await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync(request, CancellationToken.None));
            Assert.Empty(runner.Calls);
        }
    }

    [Theory]
    [MemberData(nameof(InvalidArguments))]
    public async Task ResolverRejectsInvalidIdOrSourceBeforeShowAndList(string value)
    {
        var runner = new RecordingRunner();
        var resolver = new WingetPackageResolver(runner, new(), new());
        foreach (var package in new[] { new PackageIdentity(value, "winget"), new PackageIdentity("Git.Git", value) })
        {
            await Assert.ThrowsAsync<ArgumentException>(() => resolver.ResolveAsync(package, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => resolver.CheckInstalledStatusAsync(package, CancellationToken.None));
            Assert.Empty(runner.Calls);
        }
    }

    [Fact]
    public async Task ValidMultiwordUnicodeInputsRemainDiscreteArguments()
    {
        var runner = new RecordingRunner();
        var service = new WingetPackageSearchService(runner, new(), new());
        await service.SearchAsync(new("Visual Studio 日本語", "My Source"), CancellationToken.None);
        Assert.Contains("Visual Studio 日本語", runner.Calls[0]);
        Assert.Equal("My Source", runner.Calls[0][^1]);

        var resolver = new WingetPackageResolver(runner, new(), new());
        await resolver.ResolveAsync(new("Git.Git", "My Source"), CancellationToken.None);
        Assert.Equal("Git.Git", runner.Calls[1][2]);
        Assert.Equal("My Source", runner.Calls[1][^1]);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resolver.ResolveAsync(new("Git.Git"), cancellation.Token));
        Assert.Equal(2, runner.Calls.Count);
    }

    private sealed class RejectAccessCache : IMemoryCache
    {
        public bool TryGetValue(object key, out object? value) => throw new InvalidOperationException("Invalid input reached the cache.");
        public ICacheEntry CreateEntry(object key) => throw new InvalidOperationException("Invalid input reached the cache.");
        public void Remove(object key) => throw new InvalidOperationException("Invalid input reached the cache.");
        public void Dispose() { }
    }

    private sealed class RecordingRunner : IWingetCommandRunner
    {
        public List<IReadOnlyList<string>> Calls { get; } = [];

        public Task<WingetCommandResult> RunAsync(string command, IReadOnlyList<string> arguments,
            CancellationToken cancellationToken, IProgress<WingetProgress>? progress = null, TimeSpan? timeout = null)
        {
            Calls.Add(arguments.ToArray());
            return Task.FromResult(new WingetCommandResult(0, string.Empty, string.Empty));
        }
    }
}
