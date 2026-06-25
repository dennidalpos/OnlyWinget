// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using OnlyWinget.Models;
using OnlyWinget.Services;
using OnlyWinget.ViewModels;
using Xunit;
using System.IO;

namespace OnlyWinget.Tests;

public sealed class BatchSelectionTests
{
    [Fact]
    public void PresetSelectAll_TracksTriStateAndSelectedActions()
    {
        var logs = new List<string>();
        var workspace = new PresetWorkspaceViewModel(
            isWingetAvailable: true,
            new LocalizationService(),
            new AppDataService(CreateTempDirectory()),
            new NoopDialogService(),
            new AppEntryService(new WingetQueryService(new WingetCommandService(static (_, _, _) => new WingetCommandResult { ExitCode = 0 }))),
            new TabService(),
            logs.Add);

        workspace.CurrentApps.Add(new AppEntry { Name = "A", Id = "A.App", Action = AppActions.Install, IsSelected = true });
        workspace.CurrentApps.Add(new AppEntry { Name = "B", Id = "B.App", Action = AppActions.Install, IsSelected = false });

        Assert.Null(workspace.AreAllPresetRowsSelected);

        workspace.AreAllPresetRowsSelected = true;
        Assert.True(workspace.CurrentApps.All(app => app.IsSelected));
        Assert.True(workspace.AreAllPresetRowsSelected);

        workspace.AreAllPresetRowsSelected = null;
        Assert.All(workspace.CurrentApps, app => Assert.False(app.IsSelected));
        Assert.False(workspace.AreAllPresetRowsSelected);

        workspace.AreAllPresetRowsSelected = true;
        workspace.SetSelectedPauseCommand.Execute(null);
        Assert.All(workspace.CurrentApps, app => Assert.Equal(AppActions.Pause, app.Action));
        Assert.Contains(logs, line => line.Contains("event=batch_action_applied scope=preset action=pause count=2", StringComparison.Ordinal));

        workspace.RemoveSelectedCommand.Execute(null);
        Assert.Empty(workspace.CurrentApps);
        Assert.Contains(logs, line => line.Contains("event=batch_removed scope=preset count=2", StringComparison.Ordinal));
    }

    [Fact]
    public void SearchSelectAll_TracksTriState()
    {
        var workspace = new SearchWorkspaceViewModel(new LocalizationService(), _ => { });
        workspace.Results.Add(new SearchResult { Id = "A", Name = "A", IsSelected = true });
        workspace.Results.Add(new SearchResult { Id = "B", Name = "B" });

        Assert.Null(workspace.AreAllSearchResultsSelected);
        Assert.Equal(1, workspace.SelectedCount);

        workspace.AreAllSearchResultsSelected = false;

        Assert.False(workspace.AreAllSearchResultsSelected);
        Assert.Equal(0, workspace.SelectedCount);

        workspace.AreAllSearchResultsSelected = true;
        workspace.AreAllSearchResultsSelected = null;

        Assert.False(workspace.AreAllSearchResultsSelected);
        Assert.Equal(0, workspace.SelectedCount);
    }

    [Fact]
    public void UpdatesSelectAll_TracksTriState()
    {
        var workspace = new UpdatesWorkspaceViewModel(new LocalizationService(), _ => { });
        workspace.Updates.Add(new UpdateEntry { Id = "A", Name = "A", IsSelected = true });
        workspace.Updates.Add(new UpdateEntry { Id = "B", Name = "B" });

        Assert.Null(workspace.AreAllUpdatesSelected);

        workspace.AreAllUpdatesSelected = true;

        Assert.True(workspace.AreAllUpdatesSelected);
        Assert.Equal(2, workspace.SelectedUpdates().Count);

        workspace.AreAllUpdatesSelected = null;

        Assert.False(workspace.AreAllUpdatesSelected);
        Assert.Empty(workspace.SelectedUpdates());
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "OnlyWinget.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class NoopDialogService : IDialogService
    {
        public string Prompt(string prompt, string title, string defaultValue = "") => string.Empty;
        public bool Confirm(string message, string title) => false;
        public void ShowError(string message, string title) { }
        public void ShowInfo(string message, string title) { }
        public void ShowWarning(string message, string title) { }
        public string? OpenFile(string title, string filter, string defaultExtension = "json") => null;
        public string? SaveFile(string title, string filter, string defaultFileName, string defaultExtension = "json") => null;
        public Task<PackageInterrogationDialogResult?> ShowPackageInterrogationAsync(PackageInterrogationRequest request, CancellationToken cancellationToken = default) => Task.FromResult<PackageInterrogationDialogResult?>(null);
        public Task<PackageInterrogationDialogResult?> ShowPackageInterrogationEditAsync(PackageInterrogationRequest request, AppEntry existingEntry, CancellationToken cancellationToken = default) => Task.FromResult<PackageInterrogationDialogResult?>(null);
    }
}
