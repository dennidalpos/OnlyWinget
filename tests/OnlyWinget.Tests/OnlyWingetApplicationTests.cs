using OnlyWinget.Application.App;
using OnlyWinget.Application.Activity;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Application.Storage;
using OnlyWinget.Application.System;
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
        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);
        Assert.True((await app.AddPackageToActivePresetAsync(new PackageIdentity("Git.Git", "winget"), CancellationToken.None)).Succeeded);
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
        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);
        await app.AddPackageToActivePresetAsync(new PackageIdentity("Git.Git", "winget"), CancellationToken.None);
        await app.SearchAsync("git", CancellationToken.None);
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

        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);
        await app.SearchAsync("git", CancellationToken.None);
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

        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);
        await app.RefreshUpdatesAsync(CancellationToken.None);
        app.ToggleAllUpdates();
        var result = await app.ApplySelectedUpdatesAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(PackageAction.Upgrade, executor.LastPlan?.Selections.Single().Action);
        Assert.Single(app.State.LastOperationResults);
        Assert.Empty(app.State.Updates);
        Assert.Contains(app.State.Activity, entry => entry.Title == "Git.Git" && entry.Message == "upgraded");
    }

    [Fact]
    public async Task ApplySelectedUpdatesRemovesSuccessfulRowsAndKeepsFailuresVisible()
    {
        var updates = new StubUpdateLoader(
            new PackageUpdate(new PackageIdentity("Git.Git", "winget"), "Git", "2.0.0", "2.1.0"),
            new PackageUpdate(new PackageIdentity("Missing.App", "winget"), "Missing", "1.0.0", "1.1.0"));
        var executor = new RecordingOperationExecutor(
            new OperationExecutionSummary(
                [
                    new OperationExecutionResult(
                        new PackageSelection(new PackageIdentity("Git.Git", "winget"), PackageAction.Upgrade),
                        new WingetCommandResult(0, "upgraded", string.Empty),
                        null),
                    new OperationExecutionResult(
                        new PackageSelection(new PackageIdentity("Missing.App", "winget"), PackageAction.Upgrade),
                        new WingetCommandResult(1, string.Empty, "No package found."),
                        new ClassifiedWingetError(WingetErrorKind.NotFound, "Package was not found."))
                ]));
        var app = CreateApplication(updates: updates, executor: executor);

        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);
        await app.RefreshUpdatesAsync(CancellationToken.None);
        app.ToggleAllUpdates();
        var result = await app.ApplySelectedUpdatesAsync(CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("Missing.App", Assert.Single(app.State.Updates).Package.Id);
        var row = Assert.Single(PresentationStateMapper.FromApplicationState(app.State).Updates.Updates);
        Assert.Equal("Failed", row.Status);
        Assert.Equal("Package was not found.", row.ErrorDetails);
    }

    [Fact]
    public async Task RefreshUpdatesKeepsSuccessfulSourcesAndReportsPartialFailures()
    {
        var updates = new StubUpdateLoader(
            new PackageUpdate(new PackageIdentity("Git.Git", "winget"), "Git", "2.0.0", "2.1.0"));
        updates.FailingSources.Add("msstore");
        var sources = new StubSourceService(
            new WingetSource("msstore", "https://store", false, WingetSourceStatus.Available),
            new WingetSource("winget", "https://winget", false, WingetSourceStatus.Available));
        var app = CreateApplication(updates: updates, sources: sources);

        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);
        var result = await app.RefreshUpdatesAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Git.Git", Assert.Single(app.State.Updates).Package.Id);
        Assert.Contains(app.State.Activity, entry => entry.Title == "Some sources could not be refreshed");
    }

    [Fact]
    public async Task UpdatePresentationUsesActionableDefaultsBeforeExecution()
    {
        var updates = new StubUpdateLoader(
            new PackageUpdate(new PackageIdentity("Git.Git", "winget"), "Git", "2.0.0", "2.1.0"));
        var app = CreateApplication(updates: updates);

        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);
        await app.RefreshUpdatesAsync(CancellationToken.None);

        var row = Assert.Single(PresentationStateMapper.FromApplicationState(app.State).Updates.Updates);
        Assert.Equal("Architecture_Automatic", row.Architecture);
        Assert.Equal("Update_Status_Available", row.Status);
    }

    [Fact]
    public async Task PresentationStateMapsRowsAndCapabilityGating()
    {
        var search = new StubPackageSearch(
            new PackageSearchResult(new PackageIdentity("Git.Git", "winget"), "Git", "2.0.0", "Moniker: git"));
        var app = CreateApplication(search: search);

        app.AddPreset("Default");
        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);
        await app.SearchAsync("git", CancellationToken.None);
        app.ToggleAllSearchResults();

        var presentation = PresentationStateMapper.FromApplicationState(app.State);

        Assert.Equal("Default", presentation.Presets.ActivePresetName);
        Assert.Single(presentation.Search.Results);
        Assert.True(presentation.Search.Commands.Single(command => command.Id == UiCommandId.AddSearchResults).IsEnabled);
        Assert.False(presentation.Updates.Commands.Single(command => command.Id == UiCommandId.CancelOperation).IsEnabled);
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

        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);
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
    public async Task ApplySelectedUpdatesContinueOperationsOnValidationFailure()
    {
        var updates = new StubUpdateLoader(
            new PackageUpdate(new PackageIdentity("Git.Git", "winget"), "Git", "2.0.0", "2.1.0"),
            new PackageUpdate(new PackageIdentity("Missing.App", "winget"), "Missing", "1.0.0", "1.1.0"));
        var resolver = new StubPackageResolver(
            new PackageResolution(new PackageIdentity("Git.Git", "winget"), "Git", "2.1.0", "2.0.0", true, null),
            new PackageResolution(new PackageIdentity("Missing.App", "winget"), null, null, null, false, new ClassifiedWingetError(WingetErrorKind.NotFound, "Package was not found.")));
        var executor = new RecordingOperationExecutor(
            new OperationExecutionSummary(
                [
                    new OperationExecutionResult(
                        new PackageSelection(new PackageIdentity("Git.Git", "winget"), PackageAction.Upgrade),
                        new WingetCommandResult(0, "upgraded", string.Empty),
                        null)
                ]));
        var app = CreateApplication(updates: updates, resolver: resolver, executor: executor);
        app.ContinueOperationsAfterFailure = true;

        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);
        await app.RefreshUpdatesAsync(CancellationToken.None);
        app.ToggleAllUpdates();
        var result = await app.ApplySelectedUpdatesAsync(CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(2, app.State.LastOperationResults.Count);

        var gitResult = app.State.LastOperationResults.First(r => r.Selection.Package.Id == "Git.Git");
        Assert.True(gitResult.Succeeded);

        var missingResult = app.State.LastOperationResults.First(r => r.Selection.Package.Id == "Missing.App");
        Assert.False(missingResult.Succeeded);
        Assert.Equal("Package was not found.", missingResult.Error?.Message);
    }

    [Fact]
    public async Task WindowsUpdateScanAndInstallSelectedMapsResults()
    {
        var identity = new WindowsUpdateIdentity("update-1", 100);
        var windowsUpdates = new StubWindowsUpdateService(
            [
                new WindowsUpdateItem(identity, "Security update", "Fixes", "Critical", ["Security"], ["5000001"], 12_345_678, false, false)
            ],
            [
                new WindowsUpdateInstallResult(identity, "Security update", true, true, "2", null)
            ]);
        var app = CreateApplication(windowsUpdates: windowsUpdates);

        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.ScanWindowsUpdatesAsync(new WindowsUpdateOptions(), CancellationToken.None);
        app.ToggleAllWindowsUpdates();
        var result = await app.InstallSelectedWindowsUpdatesAsync(new WindowsUpdateOptions(), CancellationToken.None);
        var presentation = PresentationStateMapper.FromApplicationState(app.State);

        Assert.True(result.Succeeded);
        Assert.True(windowsUpdates.LastInstallSelection?.Single() == identity);
        var update = Assert.Single(presentation.WindowsUpdates.Updates);
        Assert.Equal("KB5000001", update.KnowledgeBaseArticles);
        Assert.Equal(12_345_678UL, update.MaxDownloadSize);
        Assert.True(Assert.Single(presentation.WindowsUpdates.Results).RebootRequired);
    }

    [Fact]
    public async Task PresentationStateMapsDashboardAndSources()
    {
        var sources = new StubSourceService(
            new WingetSource("winget", "https://cdn.winget.microsoft.com/cache", false, WingetSourceStatus.Available));
        var app = CreateApplication(sources: sources);

        app.AddPreset("Default");
        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);
        await app.AddPackageToActivePresetAsync(new PackageIdentity("Git.Git", "winget"), CancellationToken.None);

        var presentation = PresentationStateMapper.FromApplicationState(app.State);

        Assert.True(presentation.Dashboard.IsWingetAvailable);
        Assert.Equal(1, presentation.Dashboard.PresetCount);
        Assert.Equal(1, presentation.Dashboard.ActivePresetPackageCount);
        var source = Assert.Single(presentation.Sources.Sources);
        Assert.Equal("winget", source.Name);
        Assert.Equal("Source_Type_Default", source.Type);
        Assert.True(presentation.Sources.Commands.Single(command => command.Id == UiCommandId.UpdateSources).IsEnabled);
    }

    [Fact]
    public async Task WingetOperationsFailBeforeCallingServicesWhenWingetIsUnavailable()
    {
        var search = new StubPackageSearch(new PackageSearchResult(new PackageIdentity("Git.Git"), "Git", "2.0.0", null));
        var app = CreateApplication(
            capabilities: new SystemCapabilities(true, false, true, true, null),
            search: search);
        await app.RefreshCapabilitiesAsync(CancellationToken.None);

        var result = await app.SearchAsync("git", CancellationToken.None);
        var presentation = PresentationStateMapper.FromApplicationState(app.State);

        Assert.False(result.Succeeded);
        Assert.Contains("winget is not available", result.Error, StringComparison.Ordinal);
        Assert.False(presentation.Search.Commands.Single(command => command.Id == UiCommandId.SearchPackages).IsEnabled);
    }

    [Fact]
    public async Task SourceEnabledPreferencePersistsAndFiltersSearch()
    {
        var preferences = new MemorySourcePreferenceStore();
        var sources = new StubSourceService(
            new WingetSource("winget", "https://winget", false, WingetSourceStatus.Available),
            new WingetSource("msstore", "https://store", false, WingetSourceStatus.Available));
        var search = new StubPackageSearch(new PackageSearchResult(new PackageIdentity("Git.Git", "winget"), "Git", "1", null));
        var app = CreateApplication(search: search, sources: sources, sourcePreferences: preferences);

        await app.LoadWorkspaceAsync(CancellationToken.None);
        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);
        var result = await app.SetSourceEnabledAsync("msstore", false, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(["msstore"], preferences.State.DisabledSources);
        Assert.False(app.State.Sources.Single(source => source.Name == "msstore").IsEnabled);
        await app.SearchAsync("git", CancellationToken.None);
        Assert.Equal(["winget"], search.Requests.Select(request => request.Source));
    }

    [Fact]
    public async Task SearchKeepsSuccessfulResultsWhenAnotherSourceFails()
    {
        var sources = new StubSourceService(
            new WingetSource("msstore", "https://store", false, WingetSourceStatus.Available),
            new WingetSource("winget", "https://winget", false, WingetSourceStatus.Available));
        var search = new StubPackageSearch(
            new PackageSearchResult(new PackageIdentity("Git.Git", "winget"), "Git", "2.54.0", null));
        search.FailingSources.Add("msstore");
        var app = CreateApplication(search: search, sources: sources);

        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);
        var result = await app.SearchAsync("git", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Git.Git", Assert.Single(app.State.SearchResults).Package.Id);
        Assert.Contains(app.State.Activity, entry => entry.Title == "Some sources could not be searched");
    }

    [Fact]
    public async Task StartupSkipsSourcesWhenWingetIsUnavailable()
    {
        var sources = new StubSourceService(
            new WingetSource("winget", "https://winget", false, WingetSourceStatus.Available));
        var app = CreateApplication(
            capabilities: new SystemCapabilities(true, false, true, true, null),
            sources: sources);

        await new ApplicationStartupOrchestrator(app).InitializeAsync(CancellationToken.None);

        Assert.Empty(sources.Calls);
    }

    [Fact]
    public async Task StartupRefreshesFinalSourceListWhenSourceUpdateFails()
    {
        var sources = new StubSourceService(
            new WingetSource("winget", "https://winget", false, WingetSourceStatus.Available))
        {
            FailUpdate = true
        };
        var app = CreateApplication(sources: sources);

        await new ApplicationStartupOrchestrator(app).InitializeAsync(CancellationToken.None);

        Assert.Equal(["update", "list"], sources.Calls);
        Assert.Equal("winget", Assert.Single(app.State.Sources).Name);
    }

    [Fact]
    public async Task StartupHonorsCancellationBetweenStages()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var app = CreateApplication();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ApplicationStartupOrchestrator(app).InitializeAsync(cancellation.Token));
    }

    [Fact]
    public async Task ManualPackageIsNotAddedWhenRemoteValidationFails()
    {
        var resolver = new StubPackageResolver(
            new PackageResolution(
                new PackageIdentity("Not.Real", "winget"),
                null,
                null,
                null,
                false,
                new ClassifiedWingetError(WingetErrorKind.NotFound, "Package was not found.")));
        var app = CreateApplication(resolver: resolver);
        app.AddPreset("Default");
        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);

        var result = await app.AddPackageToActivePresetAsync(
            new PackageIdentity("Not.Real", "winget"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(app.State.ActivePreset?.Packages ?? []);
    }

    [Fact]
    public async Task PackageWithoutSourceIsRejectedWhenMultipleSourcesResolveIt()
    {
        var sources = new StubSourceService(
            new WingetSource("msstore", "https://store", false, WingetSourceStatus.Available),
            new WingetSource("winget", "https://winget", false, WingetSourceStatus.Available));
        var resolver = new StubPackageResolver(
            new PackageResolution(new PackageIdentity("Shared.App", "msstore"), "Shared", "1", null, true, null),
            new PackageResolution(new PackageIdentity("Shared.App", "winget"), "Shared", "1", null, true, null));
        var app = CreateApplication(sources: sources, resolver: resolver);
        app.AddPreset("Default");
        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);

        var result = await app.AddPackageToActivePresetAsync(new PackageIdentity("Shared.App"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("multiple", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(app.State.ActivePreset?.Packages ?? []);
    }

    [Fact]
    public async Task StaleSearchSelectionFromDisabledSourceDoesNotMutatePreset()
    {
        var sources = new StubSourceService(
            new WingetSource("winget", "https://winget", false, WingetSourceStatus.Available));
        var search = new StubPackageSearch(
            new PackageSearchResult(new PackageIdentity("Git.Git", "winget"), "Git", "2", null));
        var app = CreateApplication(search: search, sources: sources);
        app.AddPreset("Default");
        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);
        await app.SearchAsync("git", CancellationToken.None);
        app.ToggleAllSearchResults();
        await app.SetSourceEnabledAsync("winget", false, CancellationToken.None);

        var result = await app.AddSelectedSearchResultsToActivePresetAsync(CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(app.State.ActivePreset?.Packages ?? []);
    }

    [Fact]
    public async Task ImportWithMixedValidAndInvalidPackagesHasNoPartialMutation()
    {
        var resolver = new StubPackageResolver(
            new PackageResolution(new PackageIdentity("Valid.App", "winget"), "Valid", "1", null, true, null),
            new PackageResolution(
                new PackageIdentity("Invalid.App", "winget"),
                null,
                null,
                null,
                false,
                new ClassifiedWingetError(WingetErrorKind.NotFound, "Package was not found.")));
        var app = CreateApplication(resolver: resolver);
        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);
        const string json = """
            {"format":"onlywinget.preset.v1","preset":{"name":"Mixed","packages":[{"id":"Valid.App","source":"winget"},{"id":"Invalid.App","source":"winget"}]}}
            """;

        var result = await app.ImportPresetAsync(json, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(app.State.Workspace.Presets);
    }

    [Fact]
    public async Task ConcurrentAsyncOperationIsRejectedWithoutReplacingBusyState()
    {
        var capabilities = new BlockingSystemCapabilityService();
        var app = CreateApplication(capabilityService: capabilities);

        var first = app.RefreshCapabilitiesAsync(CancellationToken.None);
        await capabilities.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var concurrent = await app.LoadWorkspaceAsync(CancellationToken.None);

        Assert.False(concurrent.Succeeded);
        Assert.Equal("Another operation is already in progress.", concurrent.Error);
        Assert.Equal(ApplicationBusyState.CheckingCapabilities, app.State.BusyState);

        capabilities.Complete();
        Assert.True((await first).Succeeded);
        Assert.Equal(ApplicationBusyState.Idle, app.State.BusyState);
    }

    [Fact]
    public void ClearedActivityCanBeRestoredWithoutCreatingSyntheticEntries()
    {
        var app = CreateApplication();
        var entries = new[]
        {
            new ActivityEntry(DateTimeOffset.Parse("2026-07-01T10:00:00Z"), ActivitySeverity.Warning, "Source unavailable", "winget")
        };

        Assert.True(app.RestoreActivity(entries).Succeeded);
        Assert.True(app.ClearActivity().Succeeded);
        Assert.Empty(app.State.Activity);

        Assert.True(app.RestoreActivity(entries).Succeeded);
        Assert.Equal(entries, app.State.Activity);
    }

    private static OnlyWingetApplication CreateApplication(
        SystemCapabilities? capabilities = null,
        StubPackageSearch? search = null,
        StubPackageResolver? resolver = null,
        StubUpdateLoader? updates = null,
        StubWindowsUpdateService? windowsUpdates = null,
        StubSourceService? sources = null,
        RecordingOperationExecutor? executor = null,
        ISourcePreferenceStore? sourcePreferences = null,
        ISystemCapabilityService? capabilityService = null)
    {
        return new OnlyWingetApplication(
            new MemoryWorkspaceStore(),
            capabilityService ?? new StubSystemCapabilityService(capabilities),
            search ?? new StubPackageSearch(),
            resolver ?? new StubPackageResolver(),
            updates ?? new StubUpdateLoader(),
            windowsUpdates ?? new StubWindowsUpdateService([], []),
            sources ?? new StubSourceService(new WingetSource("winget", "https://cdn.winget.microsoft.com/cache", false, WingetSourceStatus.Available)),
            executor ?? new RecordingOperationExecutor(new OperationExecutionSummary([])),
            sourcePreferenceStore: sourcePreferences);
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

    private sealed class MemorySourcePreferenceStore : ISourcePreferenceStore
    {
        public SourcePreferences State { get; private set; } = SourcePreferences.Empty;

        public Task<SourcePreferences> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(State);

        public Task SaveAsync(SourcePreferences preferences, CancellationToken cancellationToken)
        {
            State = preferences;
            return Task.CompletedTask;
        }
    }

    private sealed class StubSystemCapabilityService(
        SystemCapabilities? capabilities = null) : ISystemCapabilityService
    {
        public Task<SystemCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(capabilities ?? new SystemCapabilities(true, true, true, true, null));
    }

    private sealed class BlockingSystemCapabilityService : ISystemCapabilityService
    {
        private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<SystemCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await completion.Task.WaitAsync(cancellationToken);
            return new SystemCapabilities(true, true, true, true, null);
        }

        public void Complete() => completion.TrySetResult();
    }

    private sealed class StubPackageSearch(params PackageSearchResult[] results) : IPackageSearchService
    {
        public List<PackageSearchRequest> Requests { get; } = [];

        public HashSet<string> FailingSources { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<WingetOperationOutcome<PackageSearchResult>> SearchAsync(
            PackageSearchRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (request.Source is not null && FailingSources.Contains(request.Source))
            {
                return Task.FromResult(WingetOperationOutcome<PackageSearchResult>.Failure(
                    new ClassifiedWingetError(WingetErrorKind.SourceUnavailable, "Source unavailable."),
                    string.Empty));
            }

            return Task.FromResult(WingetOperationOutcome<PackageSearchResult>.Success(results, string.Empty));
        }
    }

    private sealed class StubPackageResolver(params PackageResolution[] resolutions) : IPackageResolver
    {
        public Task<PackageResolution> ResolveAsync(PackageIdentity package, CancellationToken cancellationToken)
        {
            var resolution = resolutions.FirstOrDefault(candidate =>
                string.Equals(candidate.Package.Id, package.Id, StringComparison.OrdinalIgnoreCase) &&
                (package.Source is null || candidate.Package.Source is null ||
                    string.Equals(candidate.Package.Source, package.Source, StringComparison.OrdinalIgnoreCase)));
            return Task.FromResult(resolution ?? new PackageResolution(package, null, null, null, true, null));
        }
    }

    private sealed class StubUpdateLoader(params PackageUpdate[] updates) : IUpdateLoader
    {
        public HashSet<string> FailingSources { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<WingetOperationOutcome<PackageUpdate>> LoadUpdatesAsync(string source, CancellationToken cancellationToken) =>
            Task.FromResult(FailingSources.Contains(source)
                ? WingetOperationOutcome<PackageUpdate>.Failure(
                    new ClassifiedWingetError(WingetErrorKind.SourceUnavailable, "Source unavailable."),
                    string.Empty)
                : WingetOperationOutcome<PackageUpdate>.Success(updates, string.Empty));
    }

    private sealed class StubWindowsUpdateService(
        IReadOnlyList<WindowsUpdateItem> updates,
        IReadOnlyList<WindowsUpdateInstallResult> results) : IWindowsUpdateService
    {
        public IReadOnlyList<WindowsUpdateIdentity>? LastInstallSelection { get; private set; }

        public Task<WindowsUpdateOperationOutcome<WindowsUpdateItem>> ScanAsync(
            WindowsUpdateOptions options,
            CancellationToken cancellationToken) =>
            Task.FromResult(WindowsUpdateOperationOutcome<WindowsUpdateItem>.Success(updates, string.Empty));

        public Task<WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>> InstallAsync(
            IReadOnlyList<WindowsUpdateIdentity> selectedUpdates,
            WindowsUpdateOptions options,
            CancellationToken cancellationToken)
        {
            LastInstallSelection = selectedUpdates.ToArray();
            return Task.FromResult(WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>.Success(results, string.Empty));
        }
    }

    private sealed class StubSourceService(params WingetSource[] sources) : IWingetSourceService
    {
        public bool FailUpdate { get; init; }

        public List<string> Calls { get; } = [];

        public Task<WingetOperationOutcome<WingetSource>> ListSourcesAsync(CancellationToken cancellationToken)
        {
            Calls.Add("list");
            return Task.FromResult(WingetOperationOutcome<WingetSource>.Success(sources, string.Empty));
        }

        public Task<WingetOperationOutcome<WingetSource>> UpdateSourcesAsync(CancellationToken cancellationToken)
        {
            Calls.Add("update");
            return Task.FromResult(FailUpdate
                ? WingetOperationOutcome<WingetSource>.Failure(
                    new ClassifiedWingetError(WingetErrorKind.SourceUnavailable, "Update failed."),
                    string.Empty)
                : WingetOperationOutcome<WingetSource>.Success(sources, string.Empty));
        }

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

        public Task<OperationExecutionSummary> ExecuteAsync(
            OperationPlan plan,
            CancellationToken cancellationToken,
            IProgress<OperationProgress>? progress = null,
            bool continueAfterFailure = false)
        {
            LastPlan = plan;
            return Task.FromResult(summary);
        }
    }
}
