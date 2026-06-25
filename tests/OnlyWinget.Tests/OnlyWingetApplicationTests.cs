using OnlyWinget.Application.App;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Application.Storage;
using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Operations;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Presets;

namespace OnlyWinget.Tests;

public sealed class OnlyWingetApplicationTests
{
    [Fact]
    public async Task PresetLifecycleUpdatesWorkspaceAndSelection()
    {
        var app = CreateApplication();

        Assert.True(app.AddPreset("Default").Succeeded);
        Assert.True(app.AddPackageToActivePreset(new PackageIdentity("Git.Git", "winget")).Succeeded);
        Assert.True(app.TogglePresetPackage(new PackageIdentity("Git.Git", "winget")).Succeeded);
        Assert.True(app.RemoveSelectedPackagesFromActivePreset().Succeeded);
        await app.SaveWorkspaceAsync(CancellationToken.None);

        Assert.Empty(app.State.ActivePreset?.Packages ?? []);
        Assert.Equal("Default", app.State.ActivePreset?.Name);
        Assert.Contains(app.State.Activity, entry => entry.Title == "Workspace saved");
    }

    [Fact]
    public async Task SearchAddSelectedResolvesPackagesAndSkipsDuplicates()
    {
        var search = new StubPackageSearch(
            new PackageSearchResult(new PackageIdentity("Git.Git"), "Git", "2.0.0", null),
            new PackageSearchResult(new PackageIdentity("Microsoft.PowerToys"), "PowerToys", "1.0.0", null));
        var resolver = new StubPackageResolver(
            new PackageResolution(new PackageIdentity("Git.Git", "winget"), "Git", "2.0.0", null, true, null),
            new PackageResolution(new PackageIdentity("Microsoft.PowerToys", "winget"), "PowerToys", "1.0.0", null, true, null));
        var app = CreateApplication(search: search, resolver: resolver);

        app.AddPreset("Default");
        app.AddPackageToActivePreset(new PackageIdentity("Git.Git", "winget"));
        await app.SearchAsync("git", null, CancellationToken.None);
        app.ToggleAllSearchResults();
        var result = await app.AddSelectedSearchResultsToActivePresetAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(["Git.Git", "Microsoft.PowerToys"], app.State.ActivePreset?.Packages.Select(package => package.Id));
        Assert.Equal("winget", app.State.ActivePreset?.Packages[1].Source);
    }

    [Fact]
    public async Task ApplySelectedUpdatesCreatesUpgradePlanAndActivity()
    {
        var updates = new StubUpdateLoader(
            new PackageUpdate(new PackageIdentity("Git.Git", "winget"), "Git", "2.0.0", "2.1.0"));
        var executor = new RecordingOperationExecutor(
            new OperationExecutionSummary(
                [
                    new OperationExecutionResult(
                        new PackageSelection(new PackageIdentity("Git.Git", "winget"), PackageAction.Upgrade),
                        new WingetCommandResult(0, "upgraded", string.Empty),
                        null)
                ]));
        var app = CreateApplication(updates: updates, executor: executor);

        await app.RefreshUpdatesAsync(CancellationToken.None);
        app.ToggleAllUpdates();
        var result = await app.ApplySelectedUpdatesAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(PackageAction.Upgrade, executor.LastPlan?.Selections.Single().Action);
        Assert.Contains(app.State.Activity, entry => entry.Title == "Git.Git" && entry.Message == "upgraded");
    }

    [Fact]
    public async Task PresentationStateMapsRowsAndCommandAvailability()
    {
        var search = new StubPackageSearch(
            new PackageSearchResult(new PackageIdentity("Git.Git", "winget"), "Git", "2.0.0", "Moniker: git"));
        var app = CreateApplication(search: search);

        app.AddPreset("Default");
        await app.SearchAsync("git", null, CancellationToken.None);
        app.ToggleAllSearchResults();

        var presentation = PresentationStateMapper.FromApplicationState(app.State);

        Assert.Equal("Default", presentation.Presets.ActivePresetName);
        Assert.Single(presentation.Search.Results);
        Assert.True(presentation.Search.Commands.Single(command => command.Id == "search.addSelected").IsEnabled);
        Assert.False(presentation.Updates.Commands.Single(command => command.Id == "operation.cancel").IsEnabled);
    }

    private static OnlyWingetApplication CreateApplication(
        StubPackageSearch? search = null,
        StubPackageResolver? resolver = null,
        StubUpdateLoader? updates = null,
        RecordingOperationExecutor? executor = null)
    {
        return new OnlyWingetApplication(
            new MemoryWorkspaceStore(),
            search ?? new StubPackageSearch(),
            resolver ?? new StubPackageResolver(),
            updates ?? new StubUpdateLoader(),
            executor ?? new RecordingOperationExecutor(new OperationExecutionSummary([])));
    }

    private sealed class MemoryWorkspaceStore : IWorkspaceStore
    {
        private WorkspaceState state = WorkspaceState.Empty;

        public Task<WorkspaceState> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(state);

        public Task SaveAsync(WorkspaceState state, CancellationToken cancellationToken)
        {
            this.state = state;
            return Task.CompletedTask;
        }
    }

    private sealed class StubPackageSearch(params PackageSearchResult[] results) : IPackageSearchService
    {
        public Task<IReadOnlyList<PackageSearchResult>> SearchAsync(
            PackageSearchRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PackageSearchResult>>(results);
    }

    private sealed class StubPackageResolver(params PackageResolution[] resolutions) : IPackageResolver
    {
        public Task<PackageResolution> ResolveAsync(PackageIdentity package, CancellationToken cancellationToken)
        {
            var resolution = resolutions.FirstOrDefault(candidate =>
                string.Equals(candidate.Package.Id, package.Id, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(resolution ?? new PackageResolution(package, null, null, null, true, null));
        }
    }

    private sealed class StubUpdateLoader(params PackageUpdate[] updates) : IUpdateLoader
    {
        public Task<IReadOnlyList<PackageUpdate>> LoadUpdatesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PackageUpdate>>(updates);
    }

    private sealed class RecordingOperationExecutor(OperationExecutionSummary summary) : IOperationExecutor
    {
        public OperationPlan? LastPlan { get; private set; }

        public Task<OperationExecutionSummary> ExecuteAsync(OperationPlan plan, CancellationToken cancellationToken)
        {
            LastPlan = plan;
            return Task.FromResult(summary);
        }
    }
}
