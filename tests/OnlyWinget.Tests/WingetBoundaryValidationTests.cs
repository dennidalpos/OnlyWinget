using Microsoft.Extensions.Caching.Memory;
using OnlyWinget.Application.App;
using OnlyWinget.Application.Operations;
using OnlyWinget.Application.Storage;
using OnlyWinget.Application.System;
using OnlyWinget.Application.Winget;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Domain.Operations;
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

    [Theory]
    [MemberData(nameof(InvalidArguments))]
    public async Task SourceServiceRejectsInvalidNameOrArgumentBeforeCli(string value)
    {
        var runner = new RecordingRunner();
        var service = new WingetSourceService(runner, new(), new());

        await Assert.ThrowsAsync<ArgumentException>(() => service.AddSourceAsync(value, "https://example.test", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => service.AddSourceAsync("custom", value, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RemoveSourceAsync(value, CancellationToken.None));
        Assert.Empty(runner.Calls);
    }

    [Theory]
    [MemberData(nameof(InvalidArguments))]
    public async Task ApplicationSourceMutationsRejectInvalidNameOrArgumentBeforeService(string value)
    {
        var sourceService = new RejectingSourceService();
        var app = new OnlyWingetApplication(
            new EmptyWorkspaceStore(),
            new ElevatedCapabilityService(),
            new NoOpPackageSearchService(),
            new NoOpPackageResolver(),
            new NoOpUpdateLoader(),
            new NoOpWindowsUpdateService(),
            sourceService,
            new NoOpOperationExecutor());

        await app.RefreshCapabilitiesAsync(CancellationToken.None);

        var addBadName = await app.AddSourceAsync(value, "https://example.test", CancellationToken.None);
        var addBadArg = await app.AddSourceAsync("custom", value, CancellationToken.None);
        var removeBadName = await app.RemoveSourceAsync(value, CancellationToken.None);

        Assert.False(addBadName.Succeeded);
        Assert.False(addBadArg.Succeeded);
        Assert.False(removeBadName.Succeeded);
        Assert.Equal(0, sourceService.MutationCalls);
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

    private sealed class EmptyWorkspaceStore : IWorkspaceStore
    {
        public Task<WorkspaceState> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(WorkspaceState.Empty);
        public Task SaveAsync(WorkspaceState state, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ElevatedCapabilityService : ISystemCapabilityService
    {
        public Task<SystemCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new SystemCapabilities(true, true, true, true, null, IsElevated: true));
    }

    private sealed class NoOpPackageSearchService : IPackageSearchService
    {
        public Task<WingetOperationOutcome<PackageSearchResult>> SearchAsync(PackageSearchRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(WingetOperationOutcome<PackageSearchResult>.Success([], string.Empty));
    }

    private sealed class NoOpPackageResolver : IPackageResolver
    {
        public Task<PackageResolution> ResolveAsync(PackageIdentity package, CancellationToken cancellationToken) =>
            Task.FromResult(new PackageResolution(package, null, null, null, true, null));
        public Task<PackageInstalledStatus> CheckInstalledStatusAsync(PackageIdentity package, CancellationToken cancellationToken) =>
            Task.FromResult(new PackageInstalledStatus(false, null));
    }

    private sealed class NoOpUpdateLoader : IUpdateLoader
    {
        public Task<WingetOperationOutcome<PackageUpdate>> LoadUpdatesAsync(string source, CancellationToken cancellationToken) =>
            Task.FromResult(WingetOperationOutcome<PackageUpdate>.Success([], string.Empty));
    }

    private sealed class NoOpWindowsUpdateService : IWindowsUpdateService
    {
        public Task<WindowsUpdateOperationOutcome<WindowsUpdateItem>> ScanAsync(WindowsUpdateOptions options, CancellationToken cancellationToken) =>
            Task.FromResult(WindowsUpdateOperationOutcome<WindowsUpdateItem>.Success([], string.Empty));
        public Task<WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>> InstallAsync(
            IReadOnlyList<WindowsUpdateIdentity> selectedUpdates, WindowsUpdateOptions options,
            CancellationToken cancellationToken, IProgress<OperationProgress>? progress = null) =>
            Task.FromResult(WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>.Success([], string.Empty));
    }

    private sealed class RejectingSourceService : IWingetSourceService
    {
        public int MutationCalls { get; private set; }
        public Task<WingetOperationOutcome<WingetSource>> ListSourcesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(WingetOperationOutcome<WingetSource>.Success([], string.Empty));
        public Task<WingetOperationOutcome<WingetSource>> UpdateSourcesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(WingetOperationOutcome<WingetSource>.Success([], string.Empty));
        public Task<WingetOperationOutcome<WingetSource>> AddSourceAsync(string name, string argument, CancellationToken cancellationToken)
        {
            MutationCalls++;
            throw new InvalidOperationException("Invalid source input reached IWingetSourceService.AddSourceAsync.");
        }
        public Task<WingetOperationOutcome<WingetSource>> RemoveSourceAsync(string name, CancellationToken cancellationToken)
        {
            MutationCalls++;
            throw new InvalidOperationException("Invalid source input reached IWingetSourceService.RemoveSourceAsync.");
        }
        public Task<WingetOperationOutcome<WingetSource>> ResetSourcesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(WingetOperationOutcome<WingetSource>.Success([], string.Empty));
    }

    private sealed class NoOpOperationExecutor : IOperationExecutor
    {
        public Task<OperationExecutionSummary> ExecuteAsync(
            OperationPlan plan, CancellationToken cancellationToken, IProgress<OperationProgress>? progress = null,
            bool continueAfterFailure = false, int maxRetries = 0, bool bypassHashValidation = false) =>
            Task.FromResult(new OperationExecutionSummary([]));
    }
}
