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
            new PackageSearchResult(new PackageIdentity("Git.Git", "winget"), "Git", "2.0.0", "winget"),
            new PackageSearchResult(new PackageIdentity("Microsoft.PowerToys", "winget"), "PowerToys", "1.0.0", "winget"));
        var resolver = new StubPackageResolver(
            new PackageResolution(new PackageIdentity("Git.Git", "winget"), "Git", "2.0.0", "winget", true, null),
            new PackageResolution(new PackageIdentity("Microsoft.PowerToys", "winget"), "PowerToys", "1.0.0", "winget", true, null));
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
            new PackageSearchResult(new PackageIdentity("Git.Git", "winget"), "Git", "2.0.0", "winget"));
        var resolver = new StubPackageResolver(
            new PackageResolution(new PackageIdentity("Git.Git", "winget"), "Git", "2.0.0", "winget", true, null));
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
        Assert.Equal("Operation_Status_Failed", row.Status);
        Assert.Equal("Package was not found. (Exit code: 1 / 0x00000001)", row.ErrorDetails);
    }

    [Fact]
    public async Task ApplySelectedUpdatesTreatsNoUpdatesAsSuccess()
    {
        var updates = new StubUpdateLoader(
            new PackageUpdate(new PackageIdentity("Git.Git", "winget"), "Git", "2.0.0", "2.1.0"),
            new PackageUpdate(new PackageIdentity("NotApplicable.App", "winget"), "NotApplicable", "1.0.0", "1.1.0"));
        var executor = new RecordingOperationExecutor(
            new OperationExecutionSummary(
                [
                    new OperationExecutionResult(
                        new PackageSelection(new PackageIdentity("Git.Git", "winget"), PackageAction.Upgrade),
                        new WingetCommandResult(0, "upgraded", string.Empty),
                        null),
                    new OperationExecutionResult(
                        new PackageSelection(new PackageIdentity("NotApplicable.App", "winget"), PackageAction.Upgrade),
                        new WingetCommandResult(1, string.Empty, "Non è stato trovato alcun aggiornamento applicabile."),
                        new ClassifiedWingetError(WingetErrorKind.NoUpdates, "Non è stato trovato alcun aggiornamento applicabile."))
                ]));
        var app = CreateApplication(updates: updates, executor: executor);

        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);
        await app.RefreshUpdatesAsync(CancellationToken.None);
        app.ToggleAllUpdates();
        var result = await app.ApplySelectedUpdatesAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Empty(app.State.Updates);
        Assert.Equal(2, app.State.LastOperationResults.Count);
        Assert.True(app.State.LastOperationResults[0].Succeeded);
        Assert.True(app.State.LastOperationResults[1].Succeeded);

        var presentation = PresentationStateMapper.FromApplicationState(app.State);
        Assert.Equal(2, presentation.Updates.OperationResults.Count);
        var gitResult = presentation.Updates.OperationResults.First(r => r.PackageId == "Git.Git");
        var warningResult = presentation.Updates.OperationResults.First(r => r.PackageId == "NotApplicable.App");

        Assert.Equal("Operation_Status_Succeeded", gitResult.Status);
        Assert.False(gitResult.IsWarning);

        Assert.Equal("Operation_Status_Warning", warningResult.Status);
        Assert.True(warningResult.IsWarning);

        var warningActivity = app.State.Activity.FirstOrDefault(a => a.Title == "NotApplicable.App");
        Assert.NotNull(warningActivity);
        Assert.Equal(ActivitySeverity.Warning, warningActivity.Severity);
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
        Assert.Equal("Value_Unknown", row.Publisher);
        Assert.Equal("Update_Status_Available", row.Status);
    }

    [Fact]
    public async Task PresentationStateMapsRowsAndCapabilityGating()
    {
        var search = new StubPackageSearch(
            new PackageSearchResult(new PackageIdentity("Git.Git", "winget"), "Git", "2.0.0", "Moniker: git"));
        var resolver = new StubPackageResolver(
            new PackageResolution(new PackageIdentity("Git.Git", "winget"), "Git", "2.0.0", "The Git Development Community", true, null));
        var app = CreateApplication(search: search, resolver: resolver);

        app.AddPreset("Default");
        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);
        await app.SearchAsync("git", CancellationToken.None);
        app.ToggleAllSearchResults();

        var presentation = PresentationStateMapper.FromApplicationState(app.State);

        Assert.Equal("Default", presentation.Presets.ActivePresetName);
        var row = Assert.Single(presentation.Search.Results);
        Assert.Equal("The Git Development Community", row.Publisher);
        Assert.True(presentation.Search.Commands.Single(command => command.Id == UiCommandId.AddSearchResults).IsEnabled);
        Assert.False(presentation.Updates.Commands.Single(command => command.Id == UiCommandId.CancelOperation).IsEnabled);
    }

    [Fact]
    public async Task SearchResolvesPublisherMetadataForEveryResult()
    {
        var search = new StubPackageSearch(
            new PackageSearchResult(new PackageIdentity("Git.Git", "winget"), "Git", "2.0.0", null),
            new PackageSearchResult(new PackageIdentity("Google.Chrome", "winget"), "Google Chrome", "150.0", null));
        var resolver = new StubPackageResolver(
            new PackageResolution(new PackageIdentity("Git.Git", "winget"), "Git", "2.0.0", "The Git Development Community", true, null),
            new PackageResolution(new PackageIdentity("Google.Chrome", "winget"), "Google Chrome", "150.0", "Google LLC", true, null));
        var app = CreateApplication(search: search, resolver: resolver);

        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);
        await app.SearchAsync("chrome", CancellationToken.None);

        var rows = PresentationStateMapper.FromApplicationState(app.State).Search.Results;

        Assert.Equal(2, resolver.Requests.Count);
        Assert.Contains(rows, row => row.PackageId == "Git.Git" && row.Publisher == "The Git Development Community");
        Assert.Contains(rows, row => row.PackageId == "Google.Chrome" && row.Publisher == "Google LLC");
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
        Assert.Equal("Operation_Status_Failed", row.Status);
        Assert.Equal("Package was not found. (Exit code: 1 / 0x00000001)", row.ErrorDetails);
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
        Assert.Equal(2, presentation.Sources.Sources.Count);
        var source = presentation.Sources.Sources.Single(s => s.Name == "winget");
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
    public async Task StartupRefreshesSourceList()
    {
        var sources = new StubSourceService(
            new WingetSource("winget", "https://cdn.winget.microsoft.com/cache", false, WingetSourceStatus.Available),
            new WingetSource("msstore", "https://storeedgefd.dsx.mp.microsoft.com/v9.0", false, WingetSourceStatus.Available));
        var app = CreateApplication(sources: sources);

        await new ApplicationStartupOrchestrator(app).InitializeAsync(CancellationToken.None);

        Assert.Equal(["list", "list"], sources.Calls);
        Assert.Equal(2, app.State.Sources.Count);
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

    [Fact]
    public async Task PresetsAreStoredAndExposedSortedByName()
    {
        var app = CreateApplication();
        Assert.True(app.AddPreset("ZPreset").Succeeded);
        Assert.True(app.AddPreset("APreset").Succeeded);
        Assert.True(app.AddPreset("MPreset").Succeeded);

        // Workspace normalization happens on save
        var saveResult = await app.SaveWorkspaceAsync(CancellationToken.None);
        Assert.True(saveResult.Succeeded);

        var presentation = PresentationStateMapper.FromApplicationState(app.State);
        Assert.Equal(new[] { "APreset", "MPreset", "ZPreset" }, app.State.Workspace.Presets.Select(p => p.Name));
        Assert.Equal(new[] { "APreset", "MPreset", "ZPreset" }, presentation.Presets.PresetNames);
    }

    [Fact]
    public async Task SearchUpdatePresetPresentationRowsAreSortedDeterministically()
    {
        // 1. Search Results
        var search = new StubPackageSearch(
            new PackageSearchResult(new PackageIdentity("Z.Z", "winget"), "ZName", "1.0", null),
            new PackageSearchResult(new PackageIdentity("A.A", "winget"), "AName", "1.0", null));
        var app = CreateApplication(search: search);
        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);
        await app.SearchAsync("test", CancellationToken.None);

        var presentation = PresentationStateMapper.FromApplicationState(app.State);
        Assert.Equal("AName", presentation.Search.Results[0].Name);
        Assert.Equal("ZName", presentation.Search.Results[1].Name);

        // 2. Windows Updates
        var winUpdates = new StubWindowsUpdateService(
            [
                new WindowsUpdateItem(new WindowsUpdateIdentity("ZId", 1), "ZTitle", "Description", "Critical", ["OS"], ["KB2"], 100UL, true, true),
                new WindowsUpdateItem(new WindowsUpdateIdentity("AId", 1), "ATitle", "Description", "Critical", ["OS"], ["KB1"], 100UL, true, true)
            ],
            []);
        app = CreateApplication(windowsUpdates: winUpdates);
        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.ScanWindowsUpdatesAsync(new WindowsUpdateOptions(), CancellationToken.None);

        presentation = PresentationStateMapper.FromApplicationState(app.State);
        Assert.Equal("ATitle", presentation.WindowsUpdates.Updates[0].Title);
        Assert.Equal("ZTitle", presentation.WindowsUpdates.Updates[1].Title);

        // 3. Preset Packages
        var resolver = new StubPackageResolver(
            new PackageResolution(new PackageIdentity("Z.Package", "winget"), "Z.Package", "1.0", "Publisher", true, null),
            new PackageResolution(new PackageIdentity("A.Package", "winget"), "A.Package", "1.0", "Publisher", true, null));
        app = CreateApplication(resolver: resolver);
        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);
        app.AddPreset("Default");
        await app.AddPackageToActivePresetAsync(new PackageIdentity("Z.Package", "winget"), CancellationToken.None);
        await app.AddPackageToActivePresetAsync(new PackageIdentity("A.Package", "winget"), CancellationToken.None);

        presentation = PresentationStateMapper.FromApplicationState(app.State);
        Assert.Equal("A.Package", presentation.Presets.Packages[0].PackageId);
        Assert.Equal("Z.Package", presentation.Presets.Packages[1].PackageId);
    }

    [Fact]
    public async Task PresetInstallPlansIncludeAllByDefaultAndExcludeSkipped()
    {
        var executor = new RecordingOperationExecutor(new OperationExecutionSummary([]));
        var resolver = new StubPackageResolver(
            new PackageResolution(new PackageIdentity("A.Pkg", "winget"), "A.Pkg", "1.0", "Publisher", true, null),
            new PackageResolution(new PackageIdentity("B.Pkg", "winget"), "B.Pkg", "1.0", "Publisher", true, null));
        var app = CreateApplication(executor: executor, resolver: resolver);
        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);
        app.AddPreset("Default");
        var pkg1 = new PackageIdentity("A.Pkg", "winget");
        var pkg2 = new PackageIdentity("B.Pkg", "winget");
        await app.AddPackageToActivePresetAsync(pkg1, CancellationToken.None);
        await app.AddPackageToActivePresetAsync(pkg2, CancellationToken.None);

        // All included by default
        Assert.Contains(pkg1, app.State.IncludedPresetPackages);
        Assert.Contains(pkg2, app.State.IncludedPresetPackages);

        // Toggle pkg2 to skip it
        app.TogglePresetPackageInclusion(pkg2);
        Assert.Contains(pkg1, app.State.IncludedPresetPackages);
        Assert.DoesNotContain(pkg2, app.State.IncludedPresetPackages);

        // Apply active preset (Install)
        await app.ApplyActivePresetAsync(PackageAction.Install, CancellationToken.None);

        // Plan should only contain pkg1
        Assert.NotNull(executor.LastPlan);
        var planPkg = Assert.Single(executor.LastPlan.Selections);
        Assert.Equal(pkg1.Id, planPkg.Package.Id);
    }

    [Fact]
    public void RemovingAPresetRequiresConfirmationInCommandPath()
    {
        var app = CreateApplication();
        app.AddPreset("Default");
        var presentation = PresentationStateMapper.FromApplicationState(app.State);
        var removeCmd = presentation.Presets.Commands.Single(c => c.Id == UiCommandId.RemovePreset);
        Assert.Equal("Dialog_RemovePreset_Message", removeCmd.ConfirmationResourceKey);
    }

    [Fact]
    public async Task SaveWorkspaceSavesStateToStore()
    {
        var store = new MemoryWorkspaceStore();
        var app = CreateApplication(workspaceStore: store);
        app.AddPreset("NewPreset");

        Assert.Equal(0, store.SaveCount);
        var result = await app.SaveWorkspaceAsync(CancellationToken.None);
        Assert.True(result.Succeeded);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task ExecutePlanSkipsAlreadyInstalledOrUpToDatePackages()
    {
        var resolver = new StubPackageResolver(
            new PackageResolution(new PackageIdentity("Pkg.A", "winget"), "Pkg.A", "1.0.0", "Auth", true, null)
        );
        resolver.InstalledPackages["Pkg.A"] = "1.0.0";

        var executor = new RecordingOperationExecutor(new OperationExecutionSummary([]));
        var app = CreateApplication(resolver: resolver, executor: executor);

        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);
        app.AddPreset("Default");
        var pkg = new PackageIdentity("Pkg.A", "winget");
        await app.AddPackageToActivePresetAsync(pkg, CancellationToken.None);

        var result = await app.ApplyActivePresetAsync(PackageAction.Install, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(executor.LastPlan);
        var opResult = Assert.Single(app.State.LastOperationResults);
        Assert.True(opResult.Succeeded);
        Assert.Contains("already present", opResult.CommandResult.StandardOutput);
    }

    [Fact]
    public async Task ExecutePlanDoesNotSkipIfNeedsUpdate()
    {
        var resolver = new StubPackageResolver(
            new PackageResolution(new PackageIdentity("Pkg.A", "winget"), "Pkg.A", "2.0.0", "Auth", true, null)
        );
        resolver.InstalledPackages["Pkg.A"] = "1.0.0";

        var pkgSelection = new PackageSelection(new PackageIdentity("Pkg.A", "winget"), PackageAction.Upgrade);
        var dummyResult = new WingetCommandResult(0, "Upgrade success", string.Empty);
        var executor = new RecordingOperationExecutor(new OperationExecutionSummary([
            new OperationExecutionResult(pkgSelection, dummyResult, null)
        ]));

        var app = CreateApplication(resolver: resolver, executor: executor);
        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);
        app.AddPreset("Default");
        var pkg = new PackageIdentity("Pkg.A", "winget");
        await app.AddPackageToActivePresetAsync(pkg, CancellationToken.None);

        var result = await app.ApplyActivePresetAsync(PackageAction.Upgrade, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(executor.LastPlan);
        var selection = Assert.Single(executor.LastPlan.Selections);
        Assert.Equal("Pkg.A", selection.Package.Id);
    }

    [Fact]
    public async Task ExecutePlanSuppressesPageLevelError()
    {
        var resolver = new StubPackageResolver(
            new PackageResolution(new PackageIdentity("Pkg.A", "winget"), "Pkg.A", "1.0.0", "Auth", true, null)
        );

        var pkgSelection = new PackageSelection(new PackageIdentity("Pkg.A", "winget"), PackageAction.Install);
        var dummyResult = new WingetCommandResult(-1, string.Empty, "Install error");
        var executor = new RecordingOperationExecutor(new OperationExecutionSummary([
            new OperationExecutionResult(pkgSelection, dummyResult, new ClassifiedWingetError(WingetErrorKind.Unknown, "Install error"))
        ]));

        var app = CreateApplication(resolver: resolver, executor: executor);
        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);
        app.AddPreset("Default");
        var pkg = new PackageIdentity("Pkg.A", "winget");
        await app.AddPackageToActivePresetAsync(pkg, CancellationToken.None);

        var result = await app.ApplyActivePresetAsync(PackageAction.Install, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(app.State.UserVisibleError);
        Assert.Contains(app.State.Activity, entry => entry.Severity == ActivitySeverity.Error && entry.Title == "Pkg.A");
    }

    [Fact]
    public async Task StartupConfiguresDefaultSourcesIfFirstRun()
    {
        var sources = new StubSourceService();
        var preferences = new MemorySourcePreferenceStore { State = new SourcePreferences([], DefaultSourcesConfigured: false) };
        var app = CreateApplication(sources: sources, sourcePreferences: preferences);

        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);

        Assert.Contains(sources.Calls, c => c.StartsWith("add:winget:"));
        Assert.Contains(sources.Calls, c => c.StartsWith("add:msstore:"));
        var updatedPrefs = await preferences.LoadAsync(CancellationToken.None);
        Assert.True(updatedPrefs.DefaultSourcesConfigured);
    }

    [Fact]
    public void DefaultPreferencesAreEmpty()
    {
        var preferences = SourcePreferences.Empty;
        Assert.Empty(preferences.DisabledSources);
        Assert.False(preferences.DefaultSourcesConfigured);
    }

    [Fact]
    public async Task EnsureOfficialSourcesConfigured_AddsMissingSources_WithCorrectCdnUrlForNewOsAndWinget()
    {
        var capabilities = new SystemCapabilities(
            IsSupportedOs: true,
            IsWingetAvailable: true,
            IsPowerShellAvailable: true,
            IsWindowsUpdateComAvailable: true,
            WindowsUpdateUnavailableReason: null,
            WingetVersion: "1.8.1791",
            WindowsBuildNumber: 19041
        );

        var sources = new StubSourceService();
        var app = CreateApplication(capabilities: capabilities, sources: sources);

        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        var result = await app.RefreshSourcesAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Contains(sources.Calls, c => c.StartsWith("add:winget:https://cdn.winget.microsoft.com/cache"));
        Assert.Contains(sources.Calls, c => c.StartsWith("add:msstore:https://storeedgefd.dsx.mp.microsoft.com/v9.0"));
        Assert.Contains(app.State.Sources, s => s.Name == "winget" && s.IsEnabled);
        Assert.Contains(app.State.Sources, s => s.Name == "msstore" && s.IsEnabled);
    }

    [Fact]
    public async Task EnsureOfficialSourcesConfigured_AddsMissingSources_WithLegacyAzureEdgeUrlForOldOsOrWinget()
    {
        var capabilities = new SystemCapabilities(
            IsSupportedOs: true,
            IsWingetAvailable: true,
            IsPowerShellAvailable: true,
            IsWindowsUpdateComAvailable: true,
            WindowsUpdateUnavailableReason: null,
            WingetVersion: "1.1.1234",
            WindowsBuildNumber: 17763
        );

        var sources = new StubSourceService();
        var app = CreateApplication(capabilities: capabilities, sources: sources);

        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        var result = await app.RefreshSourcesAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Contains(sources.Calls, c => c.StartsWith("add:winget:https://winget.azureedge.net/cache"));
        Assert.Contains(app.State.Sources, s => s.Name == "winget" && s.IsEnabled);
    }

    [Fact]
    public async Task EnsureOfficialSourcesConfigured_ReplacesIncorrectUrl()
    {
        var capabilities = new SystemCapabilities(
            IsSupportedOs: true,
            IsWingetAvailable: true,
            IsPowerShellAvailable: true,
            IsWindowsUpdateComAvailable: true,
            WindowsUpdateUnavailableReason: null,
            WingetVersion: "1.8.1791",
            WindowsBuildNumber: 19041
        );

        var sources = new StubSourceService(
            new WingetSource("winget", "https://winget.azureedge.net/cache", false, WingetSourceStatus.Available)
        );
        var app = CreateApplication(capabilities: capabilities, sources: sources);

        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        var result = await app.RefreshSourcesAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Contains(sources.Calls, c => c == "remove:winget");
        Assert.Contains(sources.Calls, c => c.StartsWith("add:winget:https://cdn.winget.microsoft.com/cache"));
    }

    [Fact]
    public async Task EnsureOfficialSourcesConfigured_EnablesOfficialSourcesByDefault()
    {
        var capabilities = new SystemCapabilities(
            IsSupportedOs: true,
            IsWingetAvailable: true,
            IsPowerShellAvailable: true,
            IsWindowsUpdateComAvailable: true,
            WindowsUpdateUnavailableReason: null,
            WingetVersion: "1.8.1791",
            WindowsBuildNumber: 19041
        );

        var sources = new StubSourceService(
            new WingetSource("winget", "https://cdn.winget.microsoft.com/cache", false, WingetSourceStatus.Available)
        );
        var preferences = new SourcePreferences(["winget"], DefaultSourcesConfigured: true);
        var prefStore = new MemorySourcePreferenceStore { State = preferences };
        var app = CreateApplication(capabilities: capabilities, sources: sources, sourcePreferences: prefStore);

        await app.LoadWorkspaceAsync(CancellationToken.None);
        Assert.Contains("winget", prefStore.State.DisabledSources);

        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        var result = await app.RefreshSourcesAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Empty(prefStore.State.DisabledSources);
        Assert.Contains(app.State.Sources, s => s.Name == "winget" && s.IsEnabled);
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
        ISystemCapabilityService? capabilityService = null,
        IWorkspaceStore? workspaceStore = null)
    {
        return new OnlyWingetApplication(
            workspaceStore ?? new MemoryWorkspaceStore(),
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
        public int SaveCount { get; set; }

        public Task<WorkspaceState> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(state);

        public Task SaveAsync(WorkspaceState state, CancellationToken cancellationToken)
        {
            SaveCount++;
            this.state = state;
            return Task.CompletedTask;
        }
    }

    private sealed class MemorySourcePreferenceStore : ISourcePreferenceStore
    {
        public SourcePreferences State { get; set; } = new SourcePreferences([], DefaultSourcesConfigured: true);

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
        public List<PackageIdentity> Requests { get; } = [];
        public Dictionary<string, string> InstalledPackages { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<PackageResolution> ResolveAsync(PackageIdentity package, CancellationToken cancellationToken)
        {
            Requests.Add(package);
            var resolution = resolutions.FirstOrDefault(candidate =>
                string.Equals(candidate.Package.Id, package.Id, StringComparison.OrdinalIgnoreCase) &&
                (package.Source is null || candidate.Package.Source is null ||
                    string.Equals(candidate.Package.Source, package.Source, StringComparison.OrdinalIgnoreCase)));
            return Task.FromResult(resolution ?? new PackageResolution(package, null, null, null, true, null));
        }

        public Task<PackageInstalledStatus> CheckInstalledStatusAsync(PackageIdentity package, CancellationToken cancellationToken)
        {
            if (InstalledPackages.TryGetValue(package.Id, out var version))
            {
                return Task.FromResult(new PackageInstalledStatus(true, version));
            }
            return Task.FromResult(new PackageInstalledStatus(false, null));
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

    private sealed class StubSourceService : IWingetSourceService
    {
        public bool FailUpdate { get; init; }

        public List<string> Calls { get; } = [];

        private readonly List<WingetSource> list;

        public StubSourceService(params WingetSource[] sources)
        {
            list = new List<WingetSource>(sources);
        }

        public Task<WingetOperationOutcome<WingetSource>> ListSourcesAsync(CancellationToken cancellationToken)
        {
            Calls.Add("list");
            return Task.FromResult(WingetOperationOutcome<WingetSource>.Success(list.ToArray(), string.Empty));
        }

        public Task<WingetOperationOutcome<WingetSource>> UpdateSourcesAsync(CancellationToken cancellationToken)
        {
            Calls.Add("update");
            return Task.FromResult(FailUpdate
                ? WingetOperationOutcome<WingetSource>.Failure(
                    new ClassifiedWingetError(WingetErrorKind.SourceUnavailable, "Update failed."),
                    string.Empty)
                : WingetOperationOutcome<WingetSource>.Success(list.ToArray(), string.Empty));
        }

        public Task<WingetOperationOutcome<WingetSource>> AddSourceAsync(
            string name,
            string argument,
            CancellationToken cancellationToken)
        {
            Calls.Add($"add:{name}:{argument}");
            list.RemoveAll(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
            list.Add(new WingetSource(name, argument, false, WingetSourceStatus.Available));
            return Task.FromResult(WingetOperationOutcome<WingetSource>.Success(list.ToArray(), string.Empty));
        }

        public Task<WingetOperationOutcome<WingetSource>> RemoveSourceAsync(
            string name,
            CancellationToken cancellationToken)
        {
            Calls.Add($"remove:{name}");
            list.RemoveAll(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(WingetOperationOutcome<WingetSource>.Success(list.ToArray(), string.Empty));
        }

        public Task<WingetOperationOutcome<WingetSource>> ResetSourcesAsync(CancellationToken cancellationToken)
        {
            Calls.Add("reset");
            list.Clear();
            list.Add(new WingetSource("winget", "https://cdn.winget.microsoft.com/cache", false, WingetSourceStatus.Available));
            list.Add(new WingetSource("msstore", "https://storeedgefd.dsx.mp.microsoft.com/v9.0", false, WingetSourceStatus.Available));
            return Task.FromResult(WingetOperationOutcome<WingetSource>.Success(list.ToArray(), string.Empty));
        }
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
