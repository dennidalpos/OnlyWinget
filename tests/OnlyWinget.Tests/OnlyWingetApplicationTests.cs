using OnlyWinget.Application.App;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Application.Storage;
using OnlyWinget.Application.Winget;
using OnlyWinget.Application.WindowsUpdate;
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
    public async Task SearchAddSelectedCreatesDefaultPresetWhenWorkspaceIsEmpty()
    {
        var search = new StubPackageSearch(
            new PackageSearchResult(new PackageIdentity("Git.Git"), "Git", "2.0.0", null));
        var resolver = new StubPackageResolver(
            new PackageResolution(new PackageIdentity("Git.Git", "winget"), "Git", "2.0.0", null, true, null));
        var app = CreateApplication(search: search, resolver: resolver);

        await app.SearchAsync("git", null, CancellationToken.None);
        app.ToggleAllSearchResults();
        var result = await app.AddSelectedSearchResultsToActivePresetAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Default", app.State.ActivePreset?.Name);
        Assert.Equal("Git.Git", Assert.Single(app.State.ActivePreset?.Packages ?? []).Id);
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
        Assert.Single(app.State.LastOperationResults);
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

    [Fact]
    public async Task PresentationStateMapsUpdateOperationResultDetails()
    {
        var updates = new StubUpdateLoader(
            new PackageUpdate(new PackageIdentity("Missing.App", "winget"), "Missing", "1.0.0", "1.1.0"));
        var executor = new RecordingOperationExecutor(
            new OperationExecutionSummary(
                [
                    new OperationExecutionResult(
                        new PackageSelection(new PackageIdentity("Missing.App", "winget"), PackageAction.Upgrade),
                        new WingetCommandResult(1, string.Empty, "No package found matching input criteria."),
                        new ClassifiedWingetError(WingetErrorKind.NotFound, "Package was not found."))
                ]));
        var app = CreateApplication(updates: updates, executor: executor);

        await app.RefreshUpdatesAsync(CancellationToken.None);
        app.ToggleAllUpdates();
        var result = await app.ApplySelectedUpdatesAsync(CancellationToken.None);

        var presentation = PresentationStateMapper.FromApplicationState(app.State);
        var row = Assert.Single(presentation.Updates.Updates);

        Assert.False(result.Succeeded);
        Assert.Equal("Failed", row.Status);
        Assert.Equal("Package was not found.", row.ErrorDetails);
        Assert.Single(presentation.Updates.OperationResults);
    }

    [Fact]
    public async Task WindowsUpdateScanAndInstallSelectedMapsResults()
    {
        var identity = new WindowsUpdateIdentity("update-1", 100);
        var windowsUpdates = new StubWindowsUpdateService(
            [
                new WindowsUpdateItem(identity, "Security update", "Fixes", "Critical", ["Security"], false, false)
            ],
            [
                new WindowsUpdateInstallResult(identity, "Security update", true, true, "2", null)
            ]);
        var app = CreateApplication(windowsUpdates: windowsUpdates);

        await app.ScanWindowsUpdatesAsync(CancellationToken.None);
        app.ToggleAllWindowsUpdates();
        var result = await app.InstallSelectedWindowsUpdatesAsync(CancellationToken.None);
        var presentation = PresentationStateMapper.FromApplicationState(app.State);

        Assert.True(result.Succeeded);
        Assert.True(windowsUpdates.LastInstallSelection?.Single() == identity);
        Assert.Single(presentation.WindowsUpdates.Updates);
        Assert.True(Assert.Single(presentation.WindowsUpdates.Results).RebootRequired);
    }

    [Fact]
    public async Task PresentationStateMapsDashboardAndSources()
    {
        var sources = new StubSourceService(
            new WingetSource("winget", "https://cdn.winget.microsoft.com/cache", false, WingetSourceStatus.Available));
        var app = CreateApplication(sources: sources);

        app.AddPreset("Default");
        app.AddPackageToActivePreset(new PackageIdentity("Git.Git", "winget"));
        await app.CheckWingetAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);

        var presentation = PresentationStateMapper.FromApplicationState(app.State);

        Assert.True(presentation.Dashboard.IsWingetAvailable);
        Assert.Equal(1, presentation.Dashboard.PresetCount);
        Assert.Equal(1, presentation.Dashboard.ActivePresetPackageCount);
        var source = Assert.Single(presentation.Sources.Sources);
        Assert.Equal("winget", source.Name);
        Assert.Equal("Source_Type_Default", source.Type);
        Assert.True(presentation.Sources.Commands.Single(command => command.Id == "sources.update").IsEnabled);
    }

    private static OnlyWingetApplication CreateApplication(
        StubPackageSearch? search = null,
        StubPackageResolver? resolver = null,
        StubUpdateLoader? updates = null,
        StubWindowsUpdateService? windowsUpdates = null,
        StubSourceService? sources = null,
        RecordingOperationExecutor? executor = null)
    {
        return new OnlyWingetApplication(
            new MemoryWorkspaceStore(),
            new StubCommandAvailability(),
            search ?? new StubPackageSearch(),
            resolver ?? new StubPackageResolver(),
            updates ?? new StubUpdateLoader(),
            windowsUpdates ?? new StubWindowsUpdateService([], []),
            sources ?? new StubSourceService(),
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

    private sealed class StubCommandAvailability : ICommandAvailability
    {
        public Task<bool> IsWingetAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class StubPackageSearch(params PackageSearchResult[] results) : IPackageSearchService
    {
        public Task<WingetOperationOutcome<PackageSearchResult>> SearchAsync(
            PackageSearchRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(WingetOperationOutcome<PackageSearchResult>.Success(results, string.Empty));
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
        public Task<WingetOperationOutcome<PackageUpdate>> LoadUpdatesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(WingetOperationOutcome<PackageUpdate>.Success(updates, string.Empty));
    }

    private sealed class StubWindowsUpdateService(
        IReadOnlyList<WindowsUpdateItem> updates,
        IReadOnlyList<WindowsUpdateInstallResult> results) : IWindowsUpdateService
    {
        public IReadOnlyList<WindowsUpdateIdentity>? LastInstallSelection { get; private set; }

        public Task<WindowsUpdateOperationOutcome<WindowsUpdateItem>> ScanAsync(CancellationToken cancellationToken) =>
            Task.FromResult(WindowsUpdateOperationOutcome<WindowsUpdateItem>.Success(updates, string.Empty));

        public Task<WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>> InstallAsync(
            IReadOnlyList<WindowsUpdateIdentity> selectedUpdates,
            CancellationToken cancellationToken)
        {
            LastInstallSelection = selectedUpdates.ToArray();
            return Task.FromResult(WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>.Success(results, string.Empty));
        }
    }

    private sealed class StubSourceService(params WingetSource[] sources) : IWingetSourceService
    {
        public Task<WingetOperationOutcome<WingetSource>> ListSourcesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(WingetOperationOutcome<WingetSource>.Success(sources, string.Empty));

        public Task<WingetOperationOutcome<WingetSource>> UpdateSourcesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(WingetOperationOutcome<WingetSource>.Success(sources, string.Empty));

        public Task<WingetOperationOutcome<WingetSource>> AddSourceAsync(
            string name,
            string argument,
            CancellationToken cancellationToken) =>
            Task.FromResult(WingetOperationOutcome<WingetSource>.Success(sources, string.Empty));

        public Task<WingetOperationOutcome<WingetSource>> RemoveSourceAsync(
            string name,
            CancellationToken cancellationToken) =>
            Task.FromResult(WingetOperationOutcome<WingetSource>.Success(sources, string.Empty));

        public Task<WingetOperationOutcome<WingetSource>> ResetSourcesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(WingetOperationOutcome<WingetSource>.Success(sources, string.Empty));
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
