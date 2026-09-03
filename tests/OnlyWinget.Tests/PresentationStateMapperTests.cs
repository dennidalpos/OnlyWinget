using OnlyWinget.Application.App;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Domain.Packages;
using Xunit;

namespace OnlyWinget.Tests;

public sealed class PresentationStateMapperTests
{
    private static OnlyWingetApplication CreateApp() => OnlyWingetApplicationTests.CreateDefaultApplication();

    [Fact]
    public void FromApplicationStateThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => PresentationStateMapper.FromApplicationState(null!));
    }

    [Fact]
    public void IndividualSliceMappersThrowOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => PresentationStateMapper.ToDashboardState(null!));
        Assert.Throws<ArgumentNullException>(() => PresentationStateMapper.ToPresetsState(null!));
        Assert.Throws<ArgumentNullException>(() => PresentationStateMapper.ToSearchState(null!));
        Assert.Throws<ArgumentNullException>(() => PresentationStateMapper.ToUpdatesState(null!));
        Assert.Throws<ArgumentNullException>(() => PresentationStateMapper.ToWindowsUpdateState(null!));
        Assert.Throws<ArgumentNullException>(() => PresentationStateMapper.ToSourceState(null!));
        Assert.Throws<ArgumentNullException>(() => PresentationStateMapper.ToActivityState(null!));
    }

    [Fact]
    public async Task ToDashboardStateMapsStateCorrectly()
    {
        var app = CreateApp();
        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        app.AddPreset("Workstation");

        var dashboard = PresentationStateMapper.ToDashboardState(app.State);

        Assert.True(dashboard.IsWingetAvailable);
        Assert.Equal(1, dashboard.PresetCount);
        Assert.Equal("Workstation", dashboard.ActivePresetName);
        Assert.False(dashboard.IsBusy);
    }

    [Fact]
    public async Task ToPresetsStateMapsActivePresetPackages()
    {
        var app = CreateApp();
        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);
        app.AddPreset("Dev");
        var result = await app.AddPackageToActivePresetAsync(new PackageIdentity("Git.Git", "winget"), CancellationToken.None);
        Assert.True(result.Succeeded);

        var presetsState = PresentationStateMapper.ToPresetsState(app.State);

        Assert.Equal("Dev", presetsState.ActivePresetName);
        Assert.Single(presetsState.PresetNames);
        Assert.Equal("Dev", presetsState.PresetNames[0]);
        Assert.Single(presetsState.Packages);
        Assert.Equal("Git.Git", presetsState.Packages[0].PackageId);
    }

    [Fact]
    public void ToSearchStateMapsResults()
    {
        var app = CreateApp();
        var searchState = PresentationStateMapper.ToSearchState(app.State);

        Assert.False(searchState.IsLoading);
        Assert.Empty(searchState.Results);
        Assert.NotEmpty(searchState.Commands);
    }

    [Fact]
    public void ToUpdatesStateMapsUpdates()
    {
        var app = CreateApp();
        var updatesState = PresentationStateMapper.ToUpdatesState(app.State);

        Assert.False(updatesState.IsLoading);
        Assert.False(updatesState.IsExecuting);
        Assert.Empty(updatesState.Updates);
        Assert.NotEmpty(updatesState.Commands);
    }

    [Fact]
    public void ToWindowsUpdateStateMapsWindowsUpdates()
    {
        var app = CreateApp();
        var winState = PresentationStateMapper.ToWindowsUpdateState(app.State);

        Assert.False(winState.IsScanning);
        Assert.False(winState.IsInstalling);
        Assert.Empty(winState.Updates);
        Assert.NotEmpty(winState.Commands);
    }

    [Fact]
    public async Task ToSourceStateMapsSources()
    {
        var app = CreateApp();
        await app.RefreshCapabilitiesAsync(CancellationToken.None);
        await app.RefreshSourcesAsync(CancellationToken.None);

        var sourcesState = PresentationStateMapper.ToSourceState(app.State);

        Assert.False(sourcesState.IsLoading);
        Assert.Equal(2, sourcesState.Sources.Count);
        Assert.Contains(sourcesState.Sources, s => s.Name == "winget");
    }

    [Fact]
    public void ToActivityStateMapsEntries()
    {
        var app = CreateApp();
        app.AddPreset("MyPreset");

        var activityState = PresentationStateMapper.ToActivityState(app.State);

        Assert.NotEmpty(activityState.Entries);
        Assert.NotEmpty(activityState.Commands);
    }

    [Fact]
    public void FromApplicationStateMatchesIndividualSlices()
    {
        var app = CreateApp();
        app.AddPreset("Design");

        var combined = PresentationStateMapper.FromApplicationState(app.State);
        var dashboard = PresentationStateMapper.ToDashboardState(app.State);
        var presets = PresentationStateMapper.ToPresetsState(app.State);
        var search = PresentationStateMapper.ToSearchState(app.State);
        var updates = PresentationStateMapper.ToUpdatesState(app.State);
        var winUpdates = PresentationStateMapper.ToWindowsUpdateState(app.State);
        var sources = PresentationStateMapper.ToSourceState(app.State);
        var activity = PresentationStateMapper.ToActivityState(app.State);

        Assert.Equal(dashboard.ActivePresetName, combined.Dashboard.ActivePresetName);
        Assert.Equal(presets.ActivePresetName, combined.Presets.ActivePresetName);
        Assert.Equal(search.IsLoading, combined.Search.IsLoading);
        Assert.Equal(updates.IsLoading, combined.Updates.IsLoading);
        Assert.Equal(winUpdates.IsScanning, combined.WindowsUpdates.IsScanning);
        Assert.Equal(sources.Sources.Count, combined.Sources.Sources.Count);
        Assert.Equal(activity.Entries.Count, combined.Activity.Entries.Count);
    }

    [Fact]
    public void ToActivityStateHandlesEmptyAndClearedActivity()
    {
        var app = CreateApp();
        app.AddPreset("PresetA");

        var stateWithActivity = PresentationStateMapper.ToActivityState(app.State);
        Assert.NotEmpty(stateWithActivity.Entries);
        var clearCommand = Assert.Single(stateWithActivity.Commands, c => c.Id == UiCommandId.ClearActivity);
        Assert.True(clearCommand.IsEnabled);

        var snapshot = app.State.Activity.ToArray();
        app.ClearActivity();

        var emptyState = PresentationStateMapper.ToActivityState(app.State);
        Assert.Empty(emptyState.Entries);
        var disabledClear = Assert.Single(emptyState.Commands, c => c.Id == UiCommandId.ClearActivity);
        Assert.False(disabledClear.IsEnabled);

        app.RestoreActivity(snapshot);
        var restoredState = PresentationStateMapper.ToActivityState(app.State);
        Assert.Equal(snapshot.Length, restoredState.Entries.Count);
    }

    [Fact]
    public void ToDashboardStateIncludesRecentActivityInReverseOrder()
    {
        var app = CreateApp();
        for (int i = 1; i <= 7; i++)
        {
            app.AddPreset($"Preset_{i}");
        }

        var dashboard = PresentationStateMapper.ToDashboardState(app.State);
        Assert.Equal(5, dashboard.RecentActivity.Count);
        Assert.Equal("Preset_7", dashboard.RecentActivity[0].Message);
    }
}
