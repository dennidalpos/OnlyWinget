// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using OnlyWinget.Models;
using OnlyWinget.Services;
using OnlyWinget.ViewModels;
using Xunit;

namespace OnlyWinget.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public async Task ApplyCommand_DisablesMutatingUi_AndShowsProgress()
    {
        var root = CreateTempDirectory();
        try
        {
            WriteDefaultAppsList(root);

            var wingetService = CreateWingetService();
            var operationRunner = new BlockingApplyOperationRunner();
            var viewModel = CreateViewModel(root, wingetService, operationRunner, new FakeDialogService());

            viewModel.Initialize();

            Assert.True(viewModel.AddCommand.CanExecute(null));
            Assert.True(viewModel.SaveCommand.CanExecute(null));
            Assert.Equal(viewModel.Strings.Install, viewModel.AvailableActions[0].Label);
            Assert.Equal(AppActions.Install, viewModel.AvailableActions[0].Value);

            viewModel.ApplyCommand.Execute(null);
            await operationRunner.Started.Task;

            Assert.False(viewModel.AreMainActionsEnabled);
            Assert.False(viewModel.AddCommand.CanExecute(null));
            Assert.False(viewModel.SaveCommand.CanExecute(null));
            Assert.Equal(viewModel.Strings.RunningText, viewModel.StatusText);
            Assert.Equal("VS Code: 55%", viewModel.OperationProgressText);
            Assert.Equal(55, viewModel.OperationProgressValue);
            Assert.True(viewModel.IsOperationProgressVisible);
            Assert.Equal($"{viewModel.Strings.StatusInstallInProgress} 55%", viewModel.CurrentApps[0].Status);

            operationRunner.Release.TrySetResult();
            await WaitForConditionAsync(() => viewModel.AreMainActionsEnabled && !viewModel.IsOperationProgressVisible);

            Assert.True(viewModel.AddCommand.CanExecute(null));
            Assert.True(viewModel.SaveCommand.CanExecute(null));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CancelOperationCommand_CancelsRunningApply()
    {
        var root = CreateTempDirectory();
        try
        {
            WriteDefaultAppsList(root);

            var operationRunner = new CancellationAwareApplyOperationRunner();
            var viewModel = CreateViewModel(root, CreateWingetService(), operationRunner, new FakeDialogService());
            viewModel.Initialize();

            viewModel.ApplyCommand.Execute(null);
            await operationRunner.Started.Task;

            Assert.True(viewModel.IsOperationCancellationAvailable);
            Assert.True(viewModel.CancelOperationCommand.CanExecute(null));

            viewModel.CancelOperationCommand.Execute(null);
            await WaitForConditionAsync(() => viewModel.AreMainActionsEnabled && !viewModel.IsOperationProgressVisible);

            Assert.False(viewModel.IsOperationCancellationAvailable);
            Assert.Contains("event=operation_cancel_requested", viewModel.OutputText, StringComparison.Ordinal);
            Assert.Contains("event=fake_apply_cancelled", viewModel.OutputText, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CancelOperationCommand_CancelsRunningSearch()
    {
        var root = CreateTempDirectory();
        try
        {
            WriteEmptyAppsList(root);
            var searchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var searchCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var wingetService = new WingetService(
                (singleArg, args, onOutputLine, cancellationToken) =>
                {
                    var command = singleArg ?? args[0];
                    if (command == "--version")
                    {
                        return new WingetCommandResult { ExitCode = 0, Output = "v1.12.470" };
                    }

                    if (command == "search")
                    {
                        searchStarted.TrySetResult();
                        try
                        {
                            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).GetAwaiter().GetResult();
                        }
                        catch (OperationCanceledException)
                        {
                            searchCancelled.TrySetResult();
                            throw;
                        }
                    }

                    return new WingetCommandResult { ExitCode = 0, Output = string.Empty };
                });
            var viewModel = CreateViewModel(root, wingetService, new PassiveOperationRunner(), new FakeDialogService());
            viewModel.Initialize();
            viewModel.OpenSearchCommand.Execute(null);
            viewModel.SearchQuery = "code";

            viewModel.RunSearchCommand.Execute(null);
            await searchStarted.Task;

            Assert.True(viewModel.IsOperationCancellationAvailable);
            Assert.True(viewModel.CancelOperationCommand.CanExecute(null));

            viewModel.CancelOperationCommand.Execute(null);
            await searchCancelled.Task;
            await WaitForConditionAsync(() =>
                !viewModel.IsSearchInProgress
                && !viewModel.IsOperationCancellationAvailable
                && viewModel.OutputText.Contains("event=operation_cancelled", StringComparison.Ordinal));

            Assert.Empty(viewModel.SearchResults);
            Assert.Contains("event=operation_cancel_requested", viewModel.OutputText, StringComparison.Ordinal);
            Assert.Contains("event=operation_cancelled", viewModel.OutputText, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CancelOperationCommand_CancelsRunningUpdateRefresh()
    {
        var root = CreateTempDirectory();
        try
        {
            WriteEmptyAppsList(root);
            var refreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var refreshCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var wingetService = new WingetService(
                (singleArg, args, onOutputLine, cancellationToken) =>
                {
                    var command = singleArg ?? args[0];
                    if (command == "--version")
                    {
                        return new WingetCommandResult { ExitCode = 0, Output = "v1.12.470" };
                    }

                    if (command == "list" && args.Contains("--upgrade-available"))
                    {
                        refreshStarted.TrySetResult();
                        try
                        {
                            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).GetAwaiter().GetResult();
                        }
                        catch (OperationCanceledException)
                        {
                            refreshCancelled.TrySetResult();
                            throw;
                        }
                    }

                    return new WingetCommandResult { ExitCode = 0, Output = string.Empty };
                });
            var viewModel = CreateViewModel(root, wingetService, new PassiveOperationRunner(), new FakeDialogService());
            viewModel.Initialize();

            viewModel.OpenUpdatesCommand.Execute(null);
            await refreshStarted.Task;

            Assert.True(viewModel.IsOperationCancellationAvailable);
            Assert.True(viewModel.CancelOperationCommand.CanExecute(null));

            viewModel.CancelOperationCommand.Execute(null);
            await refreshCancelled.Task;
            await WaitForConditionAsync(() =>
                !viewModel.IsUpdatesLoading
                && !viewModel.IsOperationCancellationAvailable
                && viewModel.OutputText.Contains("event=operation_cancelled", StringComparison.Ordinal));

            Assert.Empty(viewModel.Updates);
            Assert.Contains("event=operation_cancel_requested", viewModel.OutputText, StringComparison.Ordinal);
            Assert.Contains("event=operation_cancelled", viewModel.OutputText, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TabCommands_CreateRenameAndDeleteTabs()
    {
        var root = CreateTempDirectory();
        try
        {
            var dialog = new FakeDialogService();
            dialog.EnqueuePrompt("Utilities");
            dialog.EnqueuePrompt("Utilities Renamed");

            var viewModel = CreateViewModel(root, CreateWingetService(), new PassiveOperationRunner(), dialog);
            viewModel.Initialize();

            viewModel.NewTabCommand.Execute(null);
            Assert.Equal("Utilities", viewModel.SelectedTabName);
            Assert.Contains("Utilities", viewModel.TabNames);

            viewModel.RenameTabCommand.Execute(null);
            Assert.Equal("Utilities Renamed", viewModel.SelectedTabName);
            Assert.DoesNotContain("Utilities", viewModel.TabNames);
            Assert.Contains("Utilities Renamed", viewModel.TabNames);

            viewModel.DeleteTabCommand.Execute(null);
            Assert.Single(viewModel.TabNames);
            Assert.Equal("Default", viewModel.SelectedTabName);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyCommand_BoundsOutputLogToRecentLines()
    {
        var root = CreateTempDirectory();
        try
        {
            WriteDefaultAppsList(root);
            var viewModel = CreateViewModel(root, CreateWingetService(), new VerboseApplyOperationRunner(1005), new FakeDialogService());
            viewModel.Initialize();

            viewModel.ApplyCommand.Execute(null);
            await WaitForConditionAsync(() => viewModel.OutputText.Contains("line-1004", StringComparison.Ordinal));

            var lines = viewModel.OutputText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            Assert.Equal(1000, lines.Length);
            Assert.DoesNotContain("line-0000", lines);
            Assert.Contains("line-1004", lines);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


    [Fact]
    public void NewTabCommand_PersistsCreatedPresetToDisk()
    {
        var root = CreateTempDirectory();
        try
        {
            var dialog = new FakeDialogService();
            dialog.EnqueuePrompt("Utilities");
            var viewModel = CreateViewModel(root, CreateWingetService(), new PassiveOperationRunner(), dialog);
            viewModel.Initialize();

            viewModel.NewTabCommand.Execute(null);

            var json = File.ReadAllText(Path.Combine(root, "AppsList.json"));
            Assert.Contains("\"Name\": \"Utilities\"", json, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RenameTabCommand_PersistsRenamedPresetToDisk()
    {
        var root = CreateTempDirectory();
        try
        {
            WriteTabbedAppsList(root);
            var dialog = new FakeDialogService();
            dialog.EnqueuePrompt("Utilities Renamed");
            var viewModel = CreateViewModel(root, CreateWingetService(), new PassiveOperationRunner(), dialog);
            viewModel.Initialize();
            viewModel.PresetWorkspace.SelectedTabName = "Utilities";

            viewModel.RenameTabCommand.Execute(null);

            var json = File.ReadAllText(Path.Combine(root, "AppsList.json"));
            Assert.Contains("\"Name\": \"Utilities Renamed\"", json, StringComparison.Ordinal);
            Assert.DoesNotContain("\"Name\": \"Utilities\"", json, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LanguageSelection_UpdatesStringsImmediately_AndPersistsChoice()
    {
        var root = CreateTempDirectory();
        try
        {
            var viewModel = CreateViewModel(root, CreateWingetService(), new PassiveOperationRunner(), new FakeDialogService(), systemCulture: "it-IT");
            viewModel.Initialize();
            var changedProperties = new List<string>();
            viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != null)
                {
                    changedProperties.Add(e.PropertyName);
                }
            };

            Assert.Equal("it", viewModel.SelectedLanguage?.Code);
            Assert.Equal("Aggiungi", viewModel.Strings.Add);
            Assert.Equal("Pronto", viewModel.HeaderStatusText);
            Assert.Equal("Workspace preset", viewModel.CurrentWorkspaceTitle);
            Assert.Equal("Aggiungi pacchetto selezionato", viewModel.SearchAddButtonText);
            Assert.Equal(
                viewModel.IsRunningAsAdministrator ? "Amministratore" : "Permessi standard",
                viewModel.PermissionStatusBadgeText);
            Assert.Equal("Windows 11 x64 it-IT", viewModel.OperatingSystemStatusBadgeText);
            Assert.Equal("OS rilevato: Windows 11 x64 it-IT", viewModel.OperatingSystemStatusBadgeTooltip);

            viewModel.SelectedLanguage = viewModel.AvailableLanguages.Single(option => option.Code == "en");

            Assert.Equal("en", viewModel.SelectedLanguage?.Code);
            Assert.Equal("Add", viewModel.Strings.Add);
            Assert.Equal("Language", viewModel.Strings.LanguageLabel);
            Assert.Equal("Ready", viewModel.HeaderStatusText);
            Assert.Equal("Preset workspace", viewModel.CurrentWorkspaceTitle);
            Assert.Equal("Add selected package", viewModel.SearchAddButtonText);
            Assert.Equal(
                viewModel.IsRunningAsAdministrator ? "Administrator" : "Standard permissions",
                viewModel.PermissionStatusBadgeText);
            Assert.Equal("Windows 11 x64 it-IT", viewModel.OperatingSystemStatusBadgeText);
            Assert.Equal("Detected OS: Windows 11 x64 it-IT", viewModel.OperatingSystemStatusBadgeTooltip);
            Assert.Contains(nameof(MainViewModel.HeaderStatusText), changedProperties);
            Assert.Contains(nameof(MainViewModel.CurrentWorkspaceTitle), changedProperties);
            Assert.Contains(nameof(MainViewModel.SearchAddButtonText), changedProperties);
            Assert.Contains(nameof(MainViewModel.PermissionStatusBadgeText), changedProperties);
            Assert.Contains(nameof(MainViewModel.OperatingSystemStatusBadgeTooltip), changedProperties);

            var preferences = new AppPreferencesService(root);
            Assert.Equal("en", preferences.Load().PreferredUiLanguage);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SearchFlow_LoadsResultsAndAddsSelectedEntry()
    {
        var root = CreateTempDirectory();
        try
        {
            var wingetService = new WingetService(
                wingetRunner: static (singleArg, args, onOutputLine) =>
                {
                    var command = singleArg ?? args[0];
                    return command switch
                    {
                        "--version" => new WingetCommandResult { ExitCode = 0, Output = "v1.12.470" },
                        "search" => new WingetCommandResult
                        {
                            ExitCode = 0,
                            Output = """
Found 1 package.
Name                 Id                    Version
--------------------------------------------------
Microsoft PowerToys  Microsoft.PowerToys   0.90.1
"""
                        },
                        "show" => new WingetCommandResult
                        {
                            ExitCode = 0,
                            Output = """
Trovato Microsoft PowerToys [Microsoft.PowerToys]
Versione: 0.90.1
Programma di installazione:
  Tipo di programma di installazione: exe
"""
                        },
                        _ => new WingetCommandResult { ExitCode = 0, Output = string.Empty }
                    };
                });
            var dialog = new FakeDialogService();
            dialog.EnqueueInterrogationResult(CreateInterrogationDialogResult("Microsoft.PowerToys", "Microsoft PowerToys", "0.90.1"));
            var viewModel = CreateViewModel(root, wingetService, new PassiveOperationRunner(), dialog);
            viewModel.Initialize();

            viewModel.OpenSearchCommand.Execute(null);
            viewModel.SearchQuery = "powertoys";
            viewModel.RunSearchCommand.Execute(null);
            await WaitForConditionAsync(() => viewModel.SearchResults.Count == 1);

            viewModel.SelectedSearchResults.Add(viewModel.SearchResults[0]);
            viewModel.UseSearchIdCommand.Execute(null);
            await WaitForConditionAsync(() => viewModel.CurrentApps.Any(app => app.Id == "Microsoft.PowerToys"));

            Assert.False(viewModel.IsSearchVisible);
            Assert.Contains(viewModel.CurrentApps, app => app.Id == "Microsoft.PowerToys");
            Assert.Single(dialog.PackageInterrogationRequests);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DeleteTabCommand_PersistsRemovedPresetToDisk()
    {
        var root = CreateTempDirectory();
        try
        {
            WriteTabbedAppsList(root);
            var viewModel = CreateViewModel(root, CreateWingetService(), new PassiveOperationRunner(), new FakeDialogService());
            viewModel.Initialize();
            viewModel.PresetWorkspace.SelectedTabName = "Utilities";

            viewModel.DeleteTabCommand.Execute(null);

            Assert.DoesNotContain("Utilities", viewModel.TabNames);
            var json = File.ReadAllText(Path.Combine(root, "AppsList.json"));
            Assert.DoesNotContain("\"Name\": \"Utilities\"", json, StringComparison.Ordinal);
            Assert.Contains("\"Name\": \"Default\"", json, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SearchFlow_RequiresInterrogationBeforeQueueing()
    {
        var root = CreateTempDirectory();
        try
        {
            var wingetService = CreateWingetService();
            var dialog = new FakeDialogService();
            var viewModel = CreateViewModel(root, wingetService, new PassiveOperationRunner(), dialog);
            viewModel.Initialize();

            viewModel.OpenSearchCommand.Execute(null);
            viewModel.SearchPickId = "Microsoft.PowerToys";
            viewModel.UseSearchIdCommand.Execute(null);
            await Task.Delay(100);

            Assert.Single(dialog.PackageInterrogationRequests);
            Assert.DoesNotContain(viewModel.CurrentApps, app => app.Id == "Microsoft.PowerToys");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SearchFlow_StopsSequenceAndDoesNotQueue_WhenInterrogationFails()
    {
        var root = CreateTempDirectory();
        try
        {
            var dialog = new FakeDialogService();
            dialog.EnqueueInterrogationResult(CreateInterrogationDialogResult("Microsoft.PowerToys", "Microsoft PowerToys", "0.90.1"));
            dialog.EnqueueInterrogationFailure("Package could not be resolved uniquely.", "OnlyWinget");

            var viewModel = CreateViewModel(root, CreateWingetService(), new PassiveOperationRunner(), dialog);
            viewModel.Initialize();

            viewModel.OpenSearchCommand.Execute(null);
            viewModel.SelectedSearchResults.Add(new SearchResult { Id = "Microsoft.PowerToys", Name = "Microsoft PowerToys", Version = "0.90.1", Source = "winget" });
            viewModel.SelectedSearchResults.Add(new SearchResult { Id = "Git.Git", Name = "Git", Version = "2.53.0.2", Source = "winget" });
            viewModel.UseSearchIdCommand.Execute(null);

            await WaitForConditionAsync(() => viewModel.CurrentApps.Any(app => app.Id == "Microsoft.PowerToys"));

            Assert.Contains(viewModel.CurrentApps, app => app.Id == "Microsoft.PowerToys");
            Assert.DoesNotContain(viewModel.CurrentApps, app => app.Id == "Git.Git");
            Assert.Single(dialog.ErrorCalls);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task UpdatesFlow_LoadsUpdatesAndResetsProgressAfterApply()
    {
        var root = CreateTempDirectory();
        try
        {
            var wingetService = new WingetService(
                wingetRunner: static (singleArg, args, onOutputLine) =>
                {
                    var command = singleArg ?? args[0];
                    return command switch
                    {
                        "--version" => new WingetCommandResult { ExitCode = 0, Output = "v1.12.470" },
                        "list" => new WingetCommandResult
                        {
                            ExitCode = 0,
                            Output = """
Name                 Id                    Version   Available Source
--------------------------------------------------------------------
Microsoft PowerToys  Microsoft.PowerToys   0.90.0    0.90.1    winget
"""
                        },
                        _ => new WingetCommandResult { ExitCode = 0, Output = string.Empty }
                    };
                });
            var operationRunner = new BlockingUpdatesOperationRunner();
            var viewModel = CreateViewModel(root, wingetService, operationRunner, new FakeDialogService());
            viewModel.Initialize();

            viewModel.OpenUpdatesCommand.Execute(null);
            await WaitForConditionAsync(() => viewModel.Updates.Count == 1);

            viewModel.ApplyUpdatesCommand.Execute(null);
            await operationRunner.Started.Task;

            Assert.False(viewModel.AreUpdatesActionsEnabled);
            Assert.True(viewModel.IsOperationProgressVisible);
            Assert.False(viewModel.IsOperationProgressIndeterminate);
            Assert.Equal(40, viewModel.OperationProgressValue);
            Assert.Equal("Microsoft PowerToys: 40%", viewModel.OperationProgressText);

            operationRunner.Release.TrySetResult();
            await WaitForConditionAsync(() => viewModel.AreUpdatesActionsEnabled && !viewModel.IsOperationProgressVisible);
            Assert.False(viewModel.IsOperationProgressIndeterminate);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task UpdatesFlow_ShowsIndeterminateProgress_ForInstallerPhase()
    {
        var root = CreateTempDirectory();
        try
        {
            var wingetService = new WingetService(
                wingetRunner: static (singleArg, args, onOutputLine) =>
                {
                    var command = singleArg ?? args[0];
                    return command switch
                    {
                        "--version" => new WingetCommandResult { ExitCode = 0, Output = "v1.12.470" },
                        "list" => new WingetCommandResult
                        {
                            ExitCode = 0,
                            Output = """
Name              Id               Version   Available      Source
------------------------------------------------------------------
Google Play Games Google.PlayGames 26.5.27.1 149.0.7814.0   winget
"""
                        },
                        _ => new WingetCommandResult { ExitCode = 0, Output = string.Empty }
                    };
                });
            var operationRunner = new IndeterminateUpdatesOperationRunner();
            var viewModel = CreateViewModel(root, wingetService, operationRunner, new FakeDialogService());
            viewModel.Initialize();

            viewModel.OpenUpdatesCommand.Execute(null);
            await WaitForConditionAsync(() => viewModel.Updates.Count == 1);

            viewModel.ApplyUpdatesCommand.Execute(null);
            await operationRunner.Started.Task;

            Assert.True(viewModel.IsOperationProgressVisible);
            Assert.True(viewModel.IsOperationProgressIndeterminate);
            Assert.Equal(0, viewModel.OperationProgressValue);
            Assert.Equal("Google Play Games: starting package install", viewModel.OperationProgressText);
            Assert.Equal(viewModel.Strings.StatusUpgradeInProgress, viewModel.Updates[0].Status);

            operationRunner.Release.TrySetResult();
            await WaitForConditionAsync(() => viewModel.AreUpdatesActionsEnabled && !viewModel.IsOperationProgressVisible);
            Assert.False(viewModel.IsOperationProgressIndeterminate);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task UpdatesFlow_UsesConfiguredPackageOptions_WhenApplyingUpdates()
    {
        var root = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(root, "AppsList.json"),
                """
                {
                  "Tabs": [
                    {
                      "Name": "Default",
                      "Apps": [
                        {
                          "Name": "WinRAR",
                          "Id": "RARLab.WinRAR",
                          "Source": "winget",
                          "Action": "Install",
                          "Scope": "machine",
                          "Architecture": "x64",
                          "Locale": "it",
                          "InstallerType": "exe"
                        }
                      ]
                    }
                  ]
                }
                """);
            var wingetService = new WingetService(
                wingetRunner: static (singleArg, args, onOutputLine) =>
                {
                    var command = singleArg ?? args[0];
                    return command switch
                    {
                        "--version" => new WingetCommandResult { ExitCode = 0, Output = "v1.12.470" },
                        "list" => new WingetCommandResult
                        {
                            ExitCode = 0,
                            Output = """
Name                 Id            Version   Available Source
-------------------------------------------------------------
WinRAR 7.20 (64-bit) RARLab.WinRAR 7.20.0    7.22.0    winget
"""
                        },
                        _ => new WingetCommandResult { ExitCode = 0, Output = string.Empty }
                    };
                });
            var operationRunner = new CapturingUpdatesOperationRunner();
            var viewModel = CreateViewModel(root, wingetService, operationRunner, new FakeDialogService());
            viewModel.Initialize();

            viewModel.OpenUpdatesCommand.Execute(null);
            await WaitForConditionAsync(() => viewModel.Updates.Count == 1);

            viewModel.ApplyUpdatesCommand.Execute(null);
            await WaitForConditionAsync(() => operationRunner.CapturedUpdates.Count == 1);

            var update = Assert.Single(operationRunner.CapturedUpdates);
            Assert.Equal("machine", update.Scope);
            Assert.Equal("x64", update.Architecture);
            Assert.Equal("it", update.Locale);
            Assert.Equal("exe", update.InstallerType);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task UpdatesFlow_IgnoresDisabledPresetPackageOptions()
    {
        var root = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(root, "AppsList.json"),
                """
                {
                  "Tabs": [
                    {
                      "Name": "Default",
                      "Apps": [
                        {
                          "Enabled": false,
                          "Name": "WinRAR",
                          "Id": "RARLab.WinRAR",
                          "Source": "winget",
                          "Action": "Install",
                          "Scope": "machine",
                          "Architecture": "x64",
                          "Locale": "it",
                          "InstallerType": "exe"
                        }
                      ]
                    }
                  ]
                }
                """);
            var wingetService = new WingetService(
                wingetRunner: static (singleArg, args, onOutputLine) =>
                {
                    var command = singleArg ?? args[0];
                    return command switch
                    {
                        "--version" => new WingetCommandResult { ExitCode = 0, Output = "v1.12.470" },
                        "list" => new WingetCommandResult
                        {
                            ExitCode = 0,
                            Output = """
Name                 Id            Version   Available Source
-------------------------------------------------------------
WinRAR 7.20 (64-bit) RARLab.WinRAR 7.20.0    7.22.0    winget
"""
                        },
                        _ => new WingetCommandResult { ExitCode = 0, Output = string.Empty }
                    };
                });
            var operationRunner = new CapturingUpdatesOperationRunner();
            var viewModel = CreateViewModel(root, wingetService, operationRunner, new FakeDialogService());
            viewModel.Initialize();

            viewModel.OpenUpdatesCommand.Execute(null);
            await WaitForConditionAsync(() => viewModel.Updates.Count == 1);

            viewModel.ApplyUpdatesCommand.Execute(null);
            await WaitForConditionAsync(() => operationRunner.CapturedUpdates.Count == 1);

            var update = Assert.Single(operationRunner.CapturedUpdates);
            Assert.Equal(string.Empty, update.Scope);
            Assert.Equal(string.Empty, update.Architecture);
            Assert.Equal(string.Empty, update.Locale);
            Assert.Equal(string.Empty, update.InstallerType);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task UpdatesFlow_MarksAttemptedUpdateStillAvailable_AfterRefreshKeepsSamePackage()
    {
        var root = CreateTempDirectory();
        try
        {
            var wingetService = new WingetService(
                wingetRunner: static (singleArg, args, onOutputLine) =>
                {
                    var command = singleArg ?? args[0];
                    return command switch
                    {
                        "--version" => new WingetCommandResult { ExitCode = 0, Output = "v1.12.470" },
                        "list" => new WingetCommandResult
                        {
                            ExitCode = 0,
                            Output = """
Name                 Id                    Version   Available Source
--------------------------------------------------------------------
Microsoft PowerToys  Microsoft.PowerToys   0.90.0    0.90.1    winget
"""
                        },
                        _ => new WingetCommandResult { ExitCode = 0, Output = string.Empty }
                    };
                });
            var viewModel = CreateViewModel(root, wingetService, new CompletedUpdatesOperationRunner(), new FakeDialogService(), systemCulture: "en-US");
            viewModel.Initialize();

            viewModel.OpenUpdatesCommand.Execute(null);
            await WaitForConditionAsync(() => viewModel.Updates.Count == 1);

            viewModel.ApplyUpdatesCommand.Execute(null);
            await WaitForConditionAsync(() => viewModel.AreUpdatesActionsEnabled && !viewModel.IsOperationProgressVisible);

            var update = Assert.Single(viewModel.Updates);
            Assert.Equal("Update still available", update.Status);
            Assert.Equal("Update still available", update.ErrorMessage);
            Assert.Contains("winget still reports 0.90.0 -> 0.90.1", update.Resolution, StringComparison.Ordinal);
            Assert.Contains("installer exited without changing the registered installed version", update.Resolution, StringComparison.Ordinal);
            Assert.Contains("row was deselected", update.Resolution, StringComparison.Ordinal);
            Assert.Contains("if a package log was created", update.Resolution, StringComparison.Ordinal);
            Assert.False(update.Selected);
            Assert.Contains("event=update_still_available", viewModel.OutputText, StringComparison.Ordinal);
            Assert.DoesNotContain(viewModel.Strings.StatusAlreadyUpdated, update.Status, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task UpdatesFlow_DoesNotLogStillAvailableEvent_WhenAttemptHasSpecificError()
    {
        var root = CreateTempDirectory();
        try
        {
            var wingetService = new WingetService(
                wingetRunner: static (singleArg, args, onOutputLine) =>
                {
                    var command = singleArg ?? args[0];
                    return command switch
                    {
                        "--version" => new WingetCommandResult { ExitCode = 0, Output = "v1.12.470" },
                        "list" => new WingetCommandResult
                        {
                            ExitCode = 0,
                            Output = """
Name                 Id                    Version   Available Source
--------------------------------------------------------------------
Microsoft PowerToys  Microsoft.PowerToys   0.90.0    0.90.1    winget
"""
                        },
                        _ => new WingetCommandResult { ExitCode = 0, Output = string.Empty }
                    };
                });
            var viewModel = CreateViewModel(root, wingetService, new FailedUpdatesOperationRunner(), new FakeDialogService(), systemCulture: "en-US");
            viewModel.Initialize();

            viewModel.OpenUpdatesCommand.Execute(null);
            await WaitForConditionAsync(() => viewModel.Updates.Count == 1);

            viewModel.ApplyUpdatesCommand.Execute(null);
            await WaitForConditionAsync(() => viewModel.AreUpdatesActionsEnabled && !viewModel.IsOperationProgressVisible);

            var update = Assert.Single(viewModel.Updates);
            Assert.Equal("Upgrade not applicable", update.Status);
            Assert.Equal("Upgrade not applicable", update.ErrorMessage);
            Assert.Equal("Manifest requirements do not match this system.", update.Resolution);
            Assert.DoesNotContain("event=update_still_available", viewModel.OutputText, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task UpdatesFlow_MarksMultiplePersistentUpdatesStillAvailable_InItalian()
    {
        var root = CreateTempDirectory();
        try
        {
            var wingetService = new WingetService(
                wingetRunner: static (singleArg, args, onOutputLine) =>
                {
                    var command = singleArg ?? args[0];
                    return command switch
                    {
                        "--version" => new WingetCommandResult { ExitCode = 0, Output = "v1.12.470" },
                        "list" => new WingetCommandResult
                        {
                            ExitCode = 0,
                            Output = """
Nome              Id                Versione   Disponibile  Origine
-------------------------------------------------------------------
Claude            Anthropic.Claude  1.6608.2.0 1.7196.0     winget
Google Play Games Google.PlayGames  26.5.27.1  149.0.7814.0 winget
"""
                        },
                        _ => new WingetCommandResult { ExitCode = 0, Output = string.Empty }
                    };
                });
            var viewModel = CreateViewModel(root, wingetService, new OperationRunner(wingetService, new InstallCommandBuilder(wingetService)), new FakeDialogService(), systemCulture: "it-IT");
            viewModel.Initialize();

            viewModel.OpenUpdatesCommand.Execute(null);
            await WaitForConditionAsync(() => viewModel.Updates.Count == 2);

            viewModel.ApplyUpdatesCommand.Execute(null);
            await WaitForConditionAsync(() => viewModel.AreUpdatesActionsEnabled && !viewModel.IsOperationProgressVisible);

            Assert.All(viewModel.Updates, update =>
            {
                Assert.Equal("Aggiornamento ancora disponibile", update.Status);
                Assert.False(update.Selected);
                Assert.Contains("dopo il tentativo di aggiornamento", update.Resolution, StringComparison.Ordinal);
                Assert.Contains("senza cambiare la versione installata registrata", update.Resolution, StringComparison.Ordinal);
                Assert.Contains("se e stato creato un log del pacchetto", update.Resolution, StringComparison.Ordinal);
            });
            Assert.Contains("event=update_still_available id=\"Anthropic.Claude\"", viewModel.OutputText, StringComparison.Ordinal);
            Assert.Contains("event=update_still_available id=\"Google.PlayGames\"", viewModel.OutputText, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Initialize_ShowsWarning_WhenSavedListIsMalformed()
    {
        var root = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, "AppsList.json"), "{ invalid json");
            var dialog = new FakeDialogService();
            var viewModel = CreateViewModel(root, CreateWingetService(), new PassiveOperationRunner(), dialog);

            viewModel.Initialize();

            Assert.Single(dialog.WarningCalls);
            Assert.Empty(dialog.ErrorCalls);
            Assert.Equal("Default", viewModel.SelectedTabName);
            Assert.Equal(
                string.Format(viewModel.Strings.DataLoadInvalidText, Path.Combine(root, "AppsList.json")),
                dialog.WarningCalls[0].Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SaveCommand_CreatesRecoveryBackupBeforeReplacingMalformedLibrary()
    {
        var root = CreateTempDirectory();
        try
        {
            var jsonPath = Path.Combine(root, "AppsList.json");
            const string malformedContent = "{ invalid json";
            File.WriteAllText(jsonPath, malformedContent);
            var dialog = new FakeDialogService();
            var viewModel = CreateViewModel(root, CreateWingetService(), new PassiveOperationRunner(), dialog);

            viewModel.Initialize();
            viewModel.SaveCommand.Execute(null);

            var backupPath = Assert.Single(Directory.GetFiles(root, "AppsList.json.recovery-*.bak"));
            Assert.Equal(malformedContent, File.ReadAllText(backupPath));
            Assert.Contains("\"Tabs\"", File.ReadAllText(jsonPath), StringComparison.Ordinal);
            Assert.Contains(backupPath, viewModel.OutputText, StringComparison.Ordinal);
            Assert.Single(dialog.WarningCalls);
            Assert.Single(dialog.InfoCalls);
            Assert.Empty(dialog.ErrorCalls);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Initialize_ShowsError_WhenSavedListCannotBeRead()
    {
        var root = CreateTempDirectory();
        try
        {
            var jsonPath = Path.Combine(root, "AppsList.json");
            File.WriteAllText(jsonPath, "{ }");
            using var lockHandle = new FileStream(jsonPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var dialog = new FakeDialogService();
            var viewModel = CreateViewModel(root, CreateWingetService(), new PassiveOperationRunner(), dialog);

            viewModel.Initialize();

            Assert.Empty(dialog.WarningCalls);
            Assert.Single(dialog.ErrorCalls);
            Assert.Equal("Default", viewModel.SelectedTabName);
            Assert.Equal(viewModel.Strings.DataLoadMessageTitle, dialog.ErrorCalls[0].Title);
            Assert.Contains(jsonPath, dialog.ErrorCalls[0].Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SearchCommands_DisableNoOpStates_AndWorkspaceActionDisablesWhenAlreadyOpen()
    {
        var root = CreateTempDirectory();
        try
        {
            var viewModel = CreateViewModel(root, CreateWingetService(), new PassiveOperationRunner(), new FakeDialogService());
            viewModel.Initialize();

            Assert.True(viewModel.OpenSearchCommand.CanExecute(null));

            viewModel.OpenSearchCommand.Execute(null);

            Assert.False(viewModel.OpenSearchCommand.CanExecute(null));
            Assert.False(viewModel.IsSearchWorkspaceButtonVisible);
            Assert.True(viewModel.IsUpdatesWorkspaceButtonVisible);
            Assert.True(viewModel.CloseSearchCommand.CanExecute(null));
            Assert.False(viewModel.RunSearchCommand.CanExecute(null));
            Assert.False(viewModel.UseSearchIdCommand.CanExecute(null));

            viewModel.SearchQuery = "powertoys";
            Assert.True(viewModel.RunSearchCommand.CanExecute(null));

            viewModel.SearchPickId = "Microsoft.PowerToys";
            Assert.True(viewModel.UseSearchIdCommand.CanExecute(null));

            viewModel.CloseSearchCommand.Execute(null);
            Assert.False(viewModel.CloseSearchCommand.CanExecute(null));
            Assert.True(viewModel.OpenSearchCommand.CanExecute(null));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SearchFlow_ShowsLoadingState_AndPluralizesAddCta()
    {
        var root = CreateTempDirectory();
        using var searchStarted = new ManualResetEventSlim();
        using var releaseSearch = new ManualResetEventSlim();

        try
        {
            var wingetService = new WingetService(
                wingetRunner: (singleArg, args, onOutputLine) =>
                {
                    var command = singleArg ?? args[0];
                    if (command == "--version")
                    {
                        return new WingetCommandResult { ExitCode = 0, Output = "v1.12.470" };
                    }

                    if (command == "search")
                    {
                        searchStarted.Set();
                        releaseSearch.Wait(TimeSpan.FromSeconds(5));
                        return new WingetCommandResult
                        {
                            ExitCode = 0,
                            Output = """
Found 2 packages.
Name                 Id                    Version
--------------------------------------------------
Microsoft PowerToys  Microsoft.PowerToys   0.90.1
Git                  Git.Git               2.53.0
"""
                        };
                    }

                    return new WingetCommandResult { ExitCode = 0, Output = string.Empty };
                });

            var viewModel = CreateViewModel(root, wingetService, new PassiveOperationRunner(), new FakeDialogService(), systemCulture: "en-US");
            viewModel.Initialize();
            viewModel.OpenSearchCommand.Execute(null);

            Assert.True(viewModel.IsSearchEmptyStateVisible);

            viewModel.SearchQuery = "tools";
            viewModel.RunSearchCommand.Execute(null);
            Assert.True(searchStarted.Wait(TimeSpan.FromSeconds(5)));
            await WaitForConditionAsync(() => viewModel.IsSearchInProgress);

            Assert.False(viewModel.IsSearchEmptyStateVisible);

            releaseSearch.Set();
            await WaitForConditionAsync(() => viewModel.SearchResults.Count == 2 && !viewModel.IsSearchInProgress);

            viewModel.SelectedSearchResults.Add(viewModel.SearchResults[0]);
            Assert.Equal("Add selected package", viewModel.SearchAddButtonText);

            viewModel.SelectedSearchResults.Add(viewModel.SearchResults[1]);
            Assert.Equal("Add selected packages", viewModel.SearchAddButtonText);
        }
        finally
        {
            releaseSearch.Set();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task UpdatesFlow_ShowsLoadingState_AndHidesCurrentWorkspaceAction()
    {
        var root = CreateTempDirectory();
        using var listStarted = new ManualResetEventSlim();
        using var releaseList = new ManualResetEventSlim();

        try
        {
            var wingetService = new WingetService(
                wingetRunner: (singleArg, args, onOutputLine) =>
                {
                    var command = singleArg ?? args[0];
                    if (command == "--version")
                    {
                        return new WingetCommandResult { ExitCode = 0, Output = "v1.12.470" };
                    }

                    if (command == "list")
                    {
                        listStarted.Set();
                        releaseList.Wait(TimeSpan.FromSeconds(5));
                        return new WingetCommandResult
                        {
                            ExitCode = 0,
                            Output = """
Name                 Id                    Version   Available Source
--------------------------------------------------------------------
Microsoft PowerToys  Microsoft.PowerToys   0.90.0    0.90.1    winget
"""
                        };
                    }

                    return new WingetCommandResult { ExitCode = 0, Output = string.Empty };
                });

            var viewModel = CreateViewModel(root, wingetService, new PassiveOperationRunner(), new FakeDialogService(), systemCulture: "en-US");
            viewModel.Initialize();

            viewModel.OpenUpdatesCommand.Execute(null);
            Assert.True(listStarted.Wait(TimeSpan.FromSeconds(5)));
            await WaitForConditionAsync(() => viewModel.IsUpdatesLoading);

            Assert.False(viewModel.IsUpdatesEmptyStateVisible);
            Assert.False(viewModel.IsSearchWorkspaceButtonVisible);
            Assert.False(viewModel.IsUpdatesWorkspaceButtonVisible);
            Assert.False(viewModel.IsPresetToolbarActionVisible);

            releaseList.Set();
            await WaitForConditionAsync(() => viewModel.Updates.Count == 1 && !viewModel.IsUpdatesLoading);

            Assert.False(viewModel.IsUpdatesEmptyStateVisible);

            viewModel.CloseUpdatesCommand.Execute(null);

            Assert.True(viewModel.IsSearchWorkspaceButtonVisible);
            Assert.True(viewModel.IsUpdatesWorkspaceButtonVisible);
            Assert.True(viewModel.IsPresetToolbarActionVisible);
        }
        finally
        {
            releaseList.Set();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ApplyCommand_StaysDisabledUntilPresetContainsApps()
    {
        var root = CreateTempDirectory();
        try
        {
            var viewModel = CreateViewModel(root, CreateWingetService(), new PassiveOperationRunner(), new FakeDialogService());
            viewModel.Initialize();

            Assert.Empty(viewModel.CurrentApps);
            Assert.False(viewModel.ApplyCommand.CanExecute(null));

            Assert.True(viewModel.PresetWorkspace.TryAddEntry("PowerToys", "Microsoft.PowerToys", out _, showDialog: false));
            Assert.True(viewModel.ApplyCommand.CanExecute(null));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyUpdatesCommand_DisablesWhenNoUpdatesAreSelected()
    {
        var root = CreateTempDirectory();
        try
        {
            var wingetService = new WingetService(
                wingetRunner: static (singleArg, args, onOutputLine) =>
                {
                    var command = singleArg ?? args[0];
                    return command switch
                    {
                        "--version" => new WingetCommandResult { ExitCode = 0, Output = "v1.12.470" },
                        "list" => new WingetCommandResult
                        {
                            ExitCode = 0,
                            Output = """
Name                 Id                    Version   Available Source
--------------------------------------------------------------------
Microsoft PowerToys  Microsoft.PowerToys   0.90.0    0.90.1    winget
"""
                        },
                        _ => new WingetCommandResult { ExitCode = 0, Output = string.Empty }
                    };
                });
            var viewModel = CreateViewModel(root, wingetService, new PassiveOperationRunner(), new FakeDialogService());
            viewModel.Initialize();

            Assert.True(viewModel.OpenUpdatesCommand.CanExecute(null));

            viewModel.OpenUpdatesCommand.Execute(null);
            await WaitForConditionAsync(() => viewModel.Updates.Count == 1);
            await WaitForConditionAsync(() => viewModel.ApplyUpdatesCommand.CanExecute(null));

            Assert.False(viewModel.OpenUpdatesCommand.CanExecute(null));
            Assert.True(viewModel.ApplyUpdatesCommand.CanExecute(null));

            viewModel.Updates[0].Selected = false;

            Assert.False(viewModel.ApplyUpdatesCommand.CanExecute(null));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExportPresetCommand_UsesSelectedPresetAndWritesJsonFile()
    {
        var root = CreateTempDirectory();
        try
        {
            WriteDefaultAppsList(root);
            var exportPath = Path.Combine(root, "default.onlywinget.json");
            var dialog = new FakeDialogService();
            dialog.SaveFileResponse = exportPath;
            var viewModel = CreateViewModel(root, CreateWingetService(), new PassiveOperationRunner(), dialog);

            viewModel.Initialize();

            Assert.True(viewModel.ExportPresetCommand.CanExecute(null));

            viewModel.ExportPresetCommand.Execute(null);

            Assert.Equal(exportPath, dialog.LastSaveFilePath);
            Assert.True(File.Exists(exportPath));
            var json = File.ReadAllText(exportPath);
            Assert.Contains("\"presetName\": \"Default\"", json, StringComparison.Ordinal);
            Assert.Contains("Microsoft.VisualStudioCode", json, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ImportPresetCommand_ReplacesExistingPresetWithSameName()
    {
        var root = CreateTempDirectory();
        try
        {
            WriteDefaultAppsList(root);
            var importPath = Path.Combine(root, "import.onlywinget.json");
            File.WriteAllText(
                importPath,
                """
                {
                  "formatVersion": 1,
                  "presetName": "Default",
                  "apps": [
                    { "name": "Git", "id": "Git.Git", "action": "Install" }
                  ]
                }
                """);

            var dialog = new FakeDialogService
            {
                OpenFileResponse = importPath
            };

            var viewModel = CreateViewModel(root, CreateWingetService(), new PassiveOperationRunner(), dialog);
            viewModel.Initialize();

            viewModel.ImportPresetCommand.Execute(null);

            Assert.Contains("Default", viewModel.TabNames);
            Assert.DoesNotContain("Default (imported)", viewModel.TabNames);
            Assert.Single(viewModel.TabNames);
            Assert.Equal("Default", viewModel.SelectedTabName);
            Assert.Single(viewModel.CurrentApps);
            Assert.Equal("Git.Git", viewModel.CurrentApps[0].Id);
            Assert.Single(dialog.InfoCalls);
            Assert.Equal(string.Format(viewModel.Strings.ImportPresetSuccessText, "Default"), dialog.InfoCalls[0].Message);

            var json = File.ReadAllText(Path.Combine(root, "AppsList.json"));
            Assert.Contains("\"Name\": \"Default\"", json, StringComparison.Ordinal);
            Assert.Contains("Git.Git", json, StringComparison.Ordinal);
            Assert.DoesNotContain("Microsoft.VisualStudioCode", json, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ImportPresetCommand_ShowsError_WhenImportFails()
    {
        var root = CreateTempDirectory();
        try
        {
            WriteDefaultAppsList(root);
            var importPath = Path.Combine(root, "invalid.onlywinget.json");
            File.WriteAllText(importPath, "{ invalid json");
            var dialog = new FakeDialogService
            {
                OpenFileResponse = importPath
            };

            var viewModel = CreateViewModel(root, CreateWingetService(), new PassiveOperationRunner(), dialog);
            viewModel.Initialize();

            viewModel.ImportPresetCommand.Execute(null);

            Assert.Single(dialog.ErrorCalls);
            Assert.Single(viewModel.TabNames);
            Assert.Equal("Default", viewModel.SelectedTabName);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task EditCommand_UpdatesAllFieldsOfExistingEntry_WithoutRemoveAndReAdd()
    {
        var root = CreateTempDirectory();
        try
        {
            WriteDefaultAppsList(root);
            var dialog = new FakeDialogService();
            var updatedResult = new PackageInterrogationDialogResult
            {
                Interrogation = new PackageInterrogationResult
                {
                    Success = true,
                    Id = "Microsoft.VisualStudioCode",
                    Name = "Visual Studio Code",
                    Version = "1.90.0",
                    Source = "winget",
                    InstallerType = "inno"
                },
                SelectedOptions = new SelectedInstallOptions
                {
                    Scope = "machine",
                    Architecture = "x64",
                    Locale = "en-US",
                    InstallerType = "inno",
                    InstallMode = InstallModes.Silent,
                    InstallLocation = @"C:\tools\vscode",
                    LogPath = @"C:\logs\vscode.log",
                    AdditionalCustomArgs = "/norestart",
                    OverrideArgs = string.Empty,
                    ElevationRequirement = "elevationRequired"
                }
            };
            dialog.EnqueueInterrogationResult(updatedResult);

            var viewModel = CreateViewModel(root, CreateWingetService(), new PassiveOperationRunner(), dialog);
            viewModel.Initialize();

            // Select the only app
            viewModel.PresetWorkspace.SelectedApp = viewModel.CurrentApps[0];
            var originalEntry = viewModel.CurrentApps[0];
            originalEntry.AdvancedArgumentsReviewed = false;

            viewModel.PresetWorkspace.EditCommand.Execute(null);
            await WaitForConditionAsync(() => originalEntry.Scope == "machine");

            // Should be 1 entry still — no remove+re-add
            Assert.Single(viewModel.CurrentApps);
            Assert.Same(originalEntry, viewModel.CurrentApps[0]);

            Assert.Equal("machine", originalEntry.Scope);
            Assert.Equal("x64", originalEntry.Architecture);
            Assert.Equal("en-US", originalEntry.Locale);
            Assert.Equal("inno", originalEntry.InstallerType);
            Assert.Equal(InstallModes.Silent, originalEntry.InstallMode);
            Assert.Equal(@"C:\tools\vscode", originalEntry.InstallLocation);
            Assert.Equal(@"C:\logs\vscode.log", originalEntry.LogPath);
            Assert.Equal("/norestart", originalEntry.AdditionalCustomArgs);
            Assert.True(originalEntry.AdvancedArgumentsReviewed);
            Assert.Equal("elevationRequired", originalEntry.ElevationRequirement);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ApplyCommand_TracksEnabledPresetApps()
    {
        var root = CreateTempDirectory();
        try
        {
            var viewModel = CreateViewModel(root, CreateWingetService(), new PassiveOperationRunner(), new FakeDialogService());
            viewModel.Initialize();

            viewModel.PresetWorkspace.CurrentApps.Add(new AppEntry
            {
                Enabled = false,
                Name = "PowerToys",
                Id = "Microsoft.PowerToys",
                Action = AppActions.Install
            });

            Assert.False(viewModel.ApplyCommand.CanExecute(null));

            viewModel.PresetWorkspace.CurrentApps[0].Enabled = true;

            Assert.True(viewModel.ApplyCommand.CanExecute(null));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyCommand_ExcludesDisabledPresetApps()
    {
        var root = CreateTempDirectory();
        try
        {
            WriteEmptyAppsList(root);
            var operationRunner = new CapturingApplyOperationRunner();
            var viewModel = CreateViewModel(root, CreateWingetService(), operationRunner, new FakeDialogService());
            viewModel.Initialize();
            viewModel.PresetWorkspace.CurrentApps.Add(new AppEntry
            {
                Enabled = true,
                Name = "VS Code",
                Id = "Microsoft.VisualStudioCode",
                Action = AppActions.Install
            });
            viewModel.PresetWorkspace.CurrentApps.Add(new AppEntry
            {
                Enabled = false,
                Name = "PowerToys",
                Id = "Microsoft.PowerToys",
                Action = AppActions.Install
            });

            viewModel.ApplyCommand.Execute(null);
            await WaitForConditionAsync(() => operationRunner.CapturedApps.Count == 1);

            var app = Assert.Single(operationRunner.CapturedApps);
            Assert.Equal("Microsoft.VisualStudioCode", app.Id);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyCommand_PreservesUnreviewedAdvancedArgumentsInSnapshot()
    {
        var root = CreateTempDirectory();
        try
        {
            WriteEmptyAppsList(root);
            var operationRunner = new CapturingApplyOperationRunner();
            var viewModel = CreateViewModel(root, CreateWingetService(), operationRunner, new FakeDialogService());
            viewModel.Initialize();
            viewModel.PresetWorkspace.CurrentApps.Add(new AppEntry
            {
                Enabled = true,
                Name = "Internal Tool",
                Id = "Contoso.InternalTool",
                Action = AppActions.Install,
                OverrideArgs = "/unsafe",
                AdvancedArgumentsReviewed = false
            });

            viewModel.ApplyCommand.Execute(null);
            await WaitForConditionAsync(() => operationRunner.CapturedApps.Count == 1);

            var app = Assert.Single(operationRunner.CapturedApps);
            Assert.Equal("/unsafe", app.OverrideArgs);
            Assert.False(app.AdvancedArgumentsReviewed);
            Assert.True(app.RequiresAdvancedArgumentsReview);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyCommand_BlocksImportedUnreviewedAdvancedArgumentsWithoutInvokingWinget()
    {
        var root = CreateTempDirectory();
        try
        {
            WriteEmptyAppsList(root);
            var invokedCommands = new List<string>();
            var wingetService = new WingetService(
                wingetRunner: (singleArg, args, onOutputLine) =>
                {
                    var command = singleArg ?? args[0];
                    if (!string.Equals(command, "--version", StringComparison.Ordinal))
                    {
                        invokedCommands.Add(command);
                    }

                    return new WingetCommandResult { ExitCode = 0, Output = "v1.12.470" };
                });
            var viewModel = CreateViewModel(
                root,
                wingetService,
                new OperationRunner(wingetService, new InstallCommandBuilder(wingetService)),
                new FakeDialogService(),
                systemCulture: "en-US");
            viewModel.Initialize();
            viewModel.PresetWorkspace.CurrentApps.Add(new AppEntry
            {
                Enabled = true,
                Name = "Internal Tool",
                Id = "Contoso.InternalTool",
                Action = AppActions.Install,
                OverrideArgs = "/unsafe",
                AdvancedArgumentsReviewed = false
            });

            viewModel.ApplyCommand.Execute(null);
            await WaitForConditionAsync(() => viewModel.OutputText.Contains("event=advanced_arguments_review_required", StringComparison.Ordinal));

            Assert.Empty(invokedCommands);
            var app = Assert.Single(viewModel.CurrentApps);
            Assert.Equal(viewModel.Strings.AdvancedArgumentsReviewRequiredText, app.ErrorMessage);
            Assert.Equal(viewModel.Strings.AdvancedArgumentsReviewRequiredResolution, app.Resolution);
            Assert.DoesNotContain("/unsafe", viewModel.OutputText, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SearchFlow_QueuesSeparateEntriesForEachSelectedArchitecture()
    {
        var root = CreateTempDirectory();
        try
        {
            WriteEmptyAppsList(root);
            var dialog = new FakeDialogService();
            dialog.EnqueueInterrogationResult(new PackageInterrogationDialogResult
            {
                Interrogation = new PackageInterrogationResult
                {
                    Success = true,
                    Id = "Microsoft.DotNet.Runtime.8",
                    Name = ".NET Runtime 8",
                    Version = "8.0.16",
                    Source = "winget",
                    InstallerType = "exe"
                },
                SelectedOptions = new SelectedInstallOptions
                {
                    Architecture = "x64",
                    InstallMode = InstallModes.SilentWithProgress
                },
                QueueSelections =
                [
                    new SelectedInstallOptions { Architecture = "x64", InstallMode = InstallModes.SilentWithProgress },
                    new SelectedInstallOptions { Architecture = "x86", InstallMode = InstallModes.SilentWithProgress }
                ]
            });

            var viewModel = CreateViewModel(root, CreateWingetService(), new PassiveOperationRunner(), dialog);
            viewModel.Initialize();
            viewModel.OpenSearchCommand.Execute(null);
            viewModel.SearchPickId = "Microsoft.DotNet.Runtime.8";

            viewModel.UseSearchIdCommand.Execute(null);
            await WaitForConditionAsync(() => viewModel.CurrentApps.Count == 2);

            Assert.Equal(2, viewModel.CurrentApps.Count);
            Assert.Contains(viewModel.CurrentApps, app => app.Id == "Microsoft.DotNet.Runtime.8" && app.Architecture == "x64");
            Assert.Contains(viewModel.CurrentApps, app => app.Id == "Microsoft.DotNet.Runtime.8" && app.Architecture == "x86");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PresetWorkspace_AllowsSameIdWithDifferentArchitectures_AndBlocksExactDuplicate()
    {
        var root = CreateTempDirectory();
        try
        {
            WriteEmptyAppsList(root);
            var viewModel = CreateViewModel(root, CreateWingetService(), new PassiveOperationRunner(), new FakeDialogService());
            viewModel.Initialize();

            var interrogation = new PackageInterrogationResult
            {
                Success = true,
                Id = "Microsoft.DotNet.Runtime.8",
                Name = ".NET Runtime 8",
                Version = "8.0.16",
                Source = "winget",
                InstallerType = "exe"
            };

            var firstAdd = viewModel.PresetWorkspace.TryAddEntries(
                interrogation,
                [
                    new SelectedInstallOptions { Architecture = "x64" },
                    new SelectedInstallOptions { Architecture = "x86" }
                ],
                out var initialWarning,
                showDialog: false);

            var duplicateAdd = viewModel.PresetWorkspace.TryAddEntries(
                interrogation,
                [new SelectedInstallOptions { Architecture = "x64" }],
                out var duplicateWarning,
                showDialog: false);

            Assert.True(firstAdd);
            Assert.True(string.IsNullOrWhiteSpace(initialWarning));
            Assert.False(duplicateAdd);
            Assert.Contains("[winget, x64]", duplicateWarning, StringComparison.Ordinal);
            Assert.Equal(2, viewModel.CurrentApps.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PresetWorkspace_AllowsSameIdAndArchitectureAcrossSources_AndBlocksExactSourceDuplicate()
    {
        var root = CreateTempDirectory();
        try
        {
            WriteEmptyAppsList(root);
            var viewModel = CreateViewModel(root, CreateWingetService(), new PassiveOperationRunner(), new FakeDialogService());
            viewModel.Initialize();

            var wingetInterrogation = new PackageInterrogationResult
            {
                Success = true,
                Id = "Contoso.Tool",
                Name = "Contoso Tool",
                Version = "1.0.0",
                Source = "winget",
                InstallerType = "exe"
            };
            var storeInterrogation = new PackageInterrogationResult
            {
                Success = true,
                Id = "Contoso.Tool",
                Name = "Contoso Tool",
                Version = "1.0.0",
                Source = "msstore",
                InstallerType = "exe"
            };

            var wingetAdd = viewModel.PresetWorkspace.TryAddEntries(
                wingetInterrogation,
                [new SelectedInstallOptions { Architecture = "x64" }],
                out var wingetWarning,
                showDialog: false);
            var storeAdd = viewModel.PresetWorkspace.TryAddEntries(
                storeInterrogation,
                [new SelectedInstallOptions { Architecture = "x64" }],
                out var storeWarning,
                showDialog: false);
            var duplicateStoreAdd = viewModel.PresetWorkspace.TryAddEntries(
                storeInterrogation,
                [new SelectedInstallOptions { Architecture = "x64" }],
                out var duplicateWarning,
                showDialog: false);

            Assert.True(wingetAdd);
            Assert.True(storeAdd);
            Assert.True(string.IsNullOrWhiteSpace(wingetWarning));
            Assert.True(string.IsNullOrWhiteSpace(storeWarning));
            Assert.False(duplicateStoreAdd);
            Assert.Contains("[msstore, x64]", duplicateWarning, StringComparison.Ordinal);
            Assert.Equal(2, viewModel.CurrentApps.Count);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyCommand_TracksStatusByOperationKey_WhenSameIdExistsForMultipleArchitectures()
    {
        var root = CreateTempDirectory();
        try
        {
            WriteMultiArchitectureAppsList(root);
            var operationRunner = new OperationKeyApplyRunner();
            var viewModel = CreateViewModel(root, CreateWingetService(), operationRunner, new FakeDialogService());
            viewModel.Initialize();

            viewModel.ApplyCommand.Execute(null);
            await operationRunner.Started.Task;

            var x64Entry = Assert.Single(viewModel.CurrentApps, app => app.Architecture == "x64");
            var x86Entry = Assert.Single(viewModel.CurrentApps, app => app.Architecture == "x86");
            Assert.Equal($"{viewModel.Strings.StatusInstallInProgress} 55%", x64Entry.Status);
            Assert.Equal(string.Empty, x86Entry.Status);

            operationRunner.Release.TrySetResult();
            await WaitForConditionAsync(() => viewModel.AreMainActionsEnabled && !viewModel.IsOperationProgressVisible);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task EditCommand_ShowsWarning_AndDoesNotMutate_WhenEditedIdBecomesDuplicate()
    {
        var root = CreateTempDirectory();
        try
        {
            WriteDefaultAppsList(root);
            var dialog = new FakeDialogService();
            var viewModel = CreateViewModel(root, CreateWingetService(), new PassiveOperationRunner(), dialog);
            viewModel.Initialize();
            Assert.True(viewModel.PresetWorkspace.TryAddEntry("PowerToys", "Microsoft.PowerToys", out _, showDialog: false));

            var originalEntry = viewModel.CurrentApps[0];
            viewModel.PresetWorkspace.SelectedApp = originalEntry;
            dialog.EnqueueInterrogationResult(CreateInterrogationDialogResult("Microsoft.PowerToys", "PowerToys", "0.90.1"));

            viewModel.PresetWorkspace.EditCommand.Execute(null);
            await Task.Delay(100);

            Assert.Equal("Microsoft.VisualStudioCode", originalEntry.Id);
            Assert.Equal("VS Code", originalEntry.Name);
            Assert.Equal(2, viewModel.CurrentApps.Count);
            Assert.Single(dialog.WarningCalls);
            Assert.Equal(viewModel.Strings.DuplicateIdTitle, dialog.WarningCalls[0].Title);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task EditCommand_BlocksAndDoesNotMutate_WhenSavedPackageCannotBeResolved()
    {
        var root = CreateTempDirectory();
        try
        {
            WriteDefaultAppsList(root);
            var dialog = new FakeDialogService();
            var wingetService = new WingetService(
                wingetRunner: static (singleArg, args, onOutputLine) =>
                {
                    var command = singleArg ?? args[0];
                    return command switch
                    {
                        "--version" => new WingetCommandResult { ExitCode = 0, Output = "v1.12.470" },
                        "show" => new WingetCommandResult { ExitCode = 1, Output = "not found" },
                        _ => new WingetCommandResult { ExitCode = 0, Output = string.Empty }
                    };
                });
            var viewModel = CreateViewModel(root, wingetService, new PassiveOperationRunner(), dialog);
            viewModel.Initialize();

            var originalEntry = viewModel.CurrentApps[0];
            viewModel.PresetWorkspace.SelectedApp = originalEntry;
            dialog.EnqueueInterrogationResult(CreateInterrogationDialogResult("Contoso.DoesNotExist", "Broken App", "1.0.0"));

            viewModel.PresetWorkspace.EditCommand.Execute(null);
            await Task.Delay(100);

            Assert.Equal("Microsoft.VisualStudioCode", originalEntry.Id);
            Assert.Equal("VS Code", originalEntry.Name);
            Assert.Empty(dialog.WarningCalls);
            Assert.Equal(viewModel.Strings.SavedPackageUnresolvedText, originalEntry.ErrorMessage);
            Assert.Equal(viewModel.Strings.SavedPackageUnresolvedResolution, originalEntry.Resolution);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AddCommand_AllowsManualSourceSelection()
    {
        var root = CreateTempDirectory();
        try
        {
            IReadOnlyList<string> invokedArgs = Array.Empty<string>();
            var wingetService = new WingetService(
                wingetRunner: (singleArg, args, onOutputLine) =>
                {
                    var command = singleArg ?? args[0];
                    if (command == "--version")
                    {
                        return new WingetCommandResult { ExitCode = 0, Output = "v1.12.470" };
                    }

                    if (command == "show")
                    {
                        invokedArgs = args;
                        return new WingetCommandResult { ExitCode = 0, Output = "found" };
                    }

                    return new WingetCommandResult { ExitCode = 0, Output = string.Empty };
                });
            var dialog = new FakeDialogService();
            dialog.EnqueuePrompt("Windows Camera");
            dialog.EnqueuePrompt("9WZDNCRFJBBG");
            dialog.EnqueuePrompt("msstore");

            var viewModel = CreateViewModel(root, wingetService, new PassiveOperationRunner(), dialog);
            viewModel.Initialize();

            viewModel.AddCommand.Execute(null);

            var entry = Assert.Single(viewModel.CurrentApps);
            Assert.Equal("9WZDNCRFJBBG", entry.Id);
            Assert.Equal("msstore", entry.Source);
            Assert.Contains("--source", invokedArgs);
            Assert.Contains("msstore", invokedArgs);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static MainViewModel CreateViewModel(string root, WingetService wingetService, IOperationRunner operationRunner, FakeDialogService dialogService, string systemCulture = "it-IT")
    {
        var dataService = new AppDataService(appDataRoot: root);
        var localizationService = new LocalizationService(
            new AppPreferencesService(root),
            () => CultureInfo.GetCultureInfo(systemCulture));
        return new MainViewModel(
            wingetService,
            dataService,
            localizationService,
            dialogService,
            new AppEntryService(wingetService),
            new TabService(),
            operationRunner,
            CreateOperatingSystemInfo(systemCulture));
    }

    private static OperatingSystemInfo CreateOperatingSystemInfo(string culture)
    {
        return new OperatingSystemInfo
        {
            ProductName = "Windows 11",
            Version = "10.0.22631.0",
            Build = "22631",
            NormalizedArchitecture = "x64",
            ProcessArchitecture = "x64",
            UiCultureName = culture
        };
    }

    private static WingetService CreateWingetService()
    {
        return new WingetService(
            wingetRunner: static (singleArg, args, onOutputLine) => new WingetCommandResult
            {
                ExitCode = 0,
                Output = "v1.12.470"
            });
    }

    private static void WriteDefaultAppsList(string root)
    {
        File.WriteAllText(
            Path.Combine(root, "AppsList.json"),
            """
            {
              "Tabs": [
                {
                  "Name": "Default",
                  "Apps": [
                    {
                      "Name": "VS Code",
                      "Id": "Microsoft.VisualStudioCode",
                      "Action": "Install"
                    }
                  ]
                }
              ]
            }
            """);
    }

    private static void WriteEmptyAppsList(string root)
    {
        File.WriteAllText(
            Path.Combine(root, "AppsList.json"),
            """
            {
              "Tabs": [
                {
                  "Name": "Default",
                  "Apps": []
                }
              ]
            }
            """);
    }

    private static void WriteMultiArchitectureAppsList(string root)
    {
        File.WriteAllText(
            Path.Combine(root, "AppsList.json"),
            """
            {
              "Tabs": [
                {
                  "Name": "Default",
                  "Apps": [
                    {
                      "Name": ".NET Runtime 8",
                      "Id": "Microsoft.DotNet.Runtime.8",
                      "Action": "Install",
                      "Architecture": "x64"
                    },
                    {
                      "Name": ".NET Runtime 8",
                      "Id": "Microsoft.DotNet.Runtime.8",
                      "Action": "Install",
                      "Architecture": "x86"
                    }
                  ]
                }
              ]
            }
            """);
    }

    private static void WriteTabbedAppsList(string root)
    {
        File.WriteAllText(
            Path.Combine(root, "AppsList.json"),
            """
            {
              "Tabs": [
                {
                  "Name": "Default",
                  "Apps": [
                    {
                      "Name": "VS Code",
                      "Id": "Microsoft.VisualStudioCode",
                      "Action": "Install"
                    }
                  ]
                },
                {
                  "Name": "Utilities",
                  "Apps": [
                    {
                      "Name": "PowerToys",
                      "Id": "Microsoft.PowerToys",
                      "Action": "Install"
                    }
                  ]
                }
              ]
            }
            """);
    }

    private static async Task WaitForConditionAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException("Condition not satisfied in time.");
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "OnlyWinget.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class BlockingApplyOperationRunner : IOperationRunner
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task RunApplyAsync(
            IReadOnlyList<AppEntry> apps,
            Action<string, UiStatusState> setStatusById,
            Action<string> appendOutput,
            Action<int, string> reportProgress,
            LocalizedStrings strings,
            Action<string, string, string>? setErrorById = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            setStatusById(apps[0].OperationKey, UiStatusState.FromKey(UiStatusKey.InstallInProgress, 55));
            reportProgress(55, "VS Code: 55%");
            Started.TrySetResult();
            await Release.Task;
        }

        public Task RunUpdatesAsync(
            IReadOnlyList<UpdateEntry> updates,
            Action<string, UiStatusState> setStatusById,
            Action<string> appendOutput,
            Action<int, string> reportProgress,
            LocalizedStrings strings,
            Action<string, string, string>? setErrorById = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class CancellationAwareApplyOperationRunner : IOperationRunner
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task RunApplyAsync(
            IReadOnlyList<AppEntry> apps,
            Action<string, UiStatusState> setStatusById,
            Action<string> appendOutput,
            Action<int, string> reportProgress,
            LocalizedStrings strings,
            Action<string, string, string>? setErrorById = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                appendOutput("event=fake_apply_cancelled");
            }
        }

        public Task RunUpdatesAsync(
            IReadOnlyList<UpdateEntry> updates,
            Action<string, UiStatusState> setStatusById,
            Action<string> appendOutput,
            Action<int, string> reportProgress,
            LocalizedStrings strings,
            Action<string, string, string>? setErrorById = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class OperationKeyApplyRunner : IOperationRunner
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task RunApplyAsync(
            IReadOnlyList<AppEntry> apps,
            Action<string, UiStatusState> setStatusById,
            Action<string> appendOutput,
            Action<int, string> reportProgress,
            LocalizedStrings strings,
            Action<string, string, string>? setErrorById = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            var x64Entry = apps.Single(app => app.Architecture == "x64");
            setStatusById(x64Entry.OperationKey, UiStatusState.FromKey(UiStatusKey.InstallInProgress, 55));
            reportProgress(55, $"{x64Entry.Name}: 55%");
            Started.TrySetResult();
            await Release.Task;
        }

        public Task RunUpdatesAsync(
            IReadOnlyList<UpdateEntry> updates,
            Action<string, UiStatusState> setStatusById,
            Action<string> appendOutput,
            Action<int, string> reportProgress,
            LocalizedStrings strings,
            Action<string, string, string>? setErrorById = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingUpdatesOperationRunner : IOperationRunner
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task RunApplyAsync(
            IReadOnlyList<AppEntry> apps,
            Action<string, UiStatusState> setStatusById,
            Action<string> appendOutput,
            Action<int, string> reportProgress,
            LocalizedStrings strings,
            Action<string, string, string>? setErrorById = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public async Task RunUpdatesAsync(
            IReadOnlyList<UpdateEntry> updates,
            Action<string, UiStatusState> setStatusById,
            Action<string> appendOutput,
            Action<int, string> reportProgress,
            LocalizedStrings strings,
            Action<string, string, string>? setErrorById = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            setStatusById(updates[0].Id, UiStatusState.FromKey(UiStatusKey.UpgradeInProgress, 40));
            reportProgress(40, "Microsoft PowerToys: 40%");
            Started.TrySetResult();
            await Release.Task;
        }
    }

    private sealed class IndeterminateUpdatesOperationRunner : IOperationRunner
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task RunApplyAsync(
            IReadOnlyList<AppEntry> apps,
            Action<string, UiStatusState> setStatusById,
            Action<string> appendOutput,
            Action<int, string> reportProgress,
            LocalizedStrings strings,
            Action<string, string, string>? setErrorById = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public async Task RunUpdatesAsync(
            IReadOnlyList<UpdateEntry> updates,
            Action<string, UiStatusState> setStatusById,
            Action<string> appendOutput,
            Action<int, string> reportProgress,
            LocalizedStrings strings,
            Action<string, string, string>? setErrorById = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            setStatusById(updates[0].Id, UiStatusState.FromKey(UiStatusKey.UpgradeInProgress));
            reportProgress(-1, "Google Play Games: starting package install");
            Started.TrySetResult();
            await Release.Task;
        }
    }

    private sealed class PassiveOperationRunner : IOperationRunner
    {
        public Task RunApplyAsync(
            IReadOnlyList<AppEntry> apps,
            Action<string, UiStatusState> setStatusById,
            Action<string> appendOutput,
            Action<int, string> reportProgress,
            LocalizedStrings strings,
            Action<string, string, string>? setErrorById = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RunUpdatesAsync(
            IReadOnlyList<UpdateEntry> updates,
            Action<string, UiStatusState> setStatusById,
            Action<string> appendOutput,
            Action<int, string> reportProgress,
            LocalizedStrings strings,
            Action<string, string, string>? setErrorById = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class VerboseApplyOperationRunner : IOperationRunner
    {
        private readonly int _lineCount;

        public VerboseApplyOperationRunner(int lineCount)
        {
            _lineCount = lineCount;
        }

        public Task RunApplyAsync(
            IReadOnlyList<AppEntry> apps,
            Action<string, UiStatusState> setStatusById,
            Action<string> appendOutput,
            Action<int, string> reportProgress,
            LocalizedStrings strings,
            Action<string, string, string>? setErrorById = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            for (var index = 0; index < _lineCount; index++)
            {
                appendOutput($"line-{index:0000}");
            }

            return Task.CompletedTask;
        }

        public Task RunUpdatesAsync(
            IReadOnlyList<UpdateEntry> updates,
            Action<string, UiStatusState> setStatusById,
            Action<string> appendOutput,
            Action<int, string> reportProgress,
            LocalizedStrings strings,
            Action<string, string, string>? setErrorById = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingApplyOperationRunner : IOperationRunner
    {
        public List<AppEntry> CapturedApps { get; } = new();

        public Task RunApplyAsync(
            IReadOnlyList<AppEntry> apps,
            Action<string, UiStatusState> setStatusById,
            Action<string> appendOutput,
            Action<int, string> reportProgress,
            LocalizedStrings strings,
            Action<string, string, string>? setErrorById = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            CapturedApps.AddRange(apps.Select(app => new AppEntry
            {
                Enabled = app.Enabled,
                Name = app.Name,
                Id = app.Id,
                Action = app.Action,
                Source = app.Source,
                Architecture = app.Architecture,
                AdditionalCustomArgs = app.AdditionalCustomArgs,
                OverrideArgs = app.OverrideArgs,
                AdvancedArgumentsReviewed = app.AdvancedArgumentsReviewed
            }));
            return Task.CompletedTask;
        }

        public Task RunUpdatesAsync(
            IReadOnlyList<UpdateEntry> updates,
            Action<string, UiStatusState> setStatusById,
            Action<string> appendOutput,
            Action<int, string> reportProgress,
            LocalizedStrings strings,
            Action<string, string, string>? setErrorById = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class CompletedUpdatesOperationRunner : IOperationRunner
    {
        public Task RunApplyAsync(
            IReadOnlyList<AppEntry> apps,
            Action<string, UiStatusState> setStatusById,
            Action<string> appendOutput,
            Action<int, string> reportProgress,
            LocalizedStrings strings,
            Action<string, string, string>? setErrorById = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RunUpdatesAsync(
            IReadOnlyList<UpdateEntry> updates,
            Action<string, UiStatusState> setStatusById,
            Action<string> appendOutput,
            Action<int, string> reportProgress,
            LocalizedStrings strings,
            Action<string, string, string>? setErrorById = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            for (var index = 0; index < updates.Count; index++)
            {
                var update = updates[index];
                setStatusById(update.Id, UiStatusState.FromKey(UiStatusKey.AlreadyUpdated));
                reportProgress((index + 1) * 100 / updates.Count, $"{update.Name}: 100%");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FailedUpdatesOperationRunner : IOperationRunner
    {
        public Task RunApplyAsync(
            IReadOnlyList<AppEntry> apps,
            Action<string, UiStatusState> setStatusById,
            Action<string> appendOutput,
            Action<int, string> reportProgress,
            LocalizedStrings strings,
            Action<string, string, string>? setErrorById = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RunUpdatesAsync(
            IReadOnlyList<UpdateEntry> updates,
            Action<string, UiStatusState> setStatusById,
            Action<string> appendOutput,
            Action<int, string> reportProgress,
            LocalizedStrings strings,
            Action<string, string, string>? setErrorById = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            setStatusById(updates[0].Id, UiStatusState.FromRawText("Upgrade not applicable"));
            setErrorById?.Invoke(updates[0].Id, "Upgrade not applicable", "Manifest requirements do not match this system.");
            reportProgress(100, $"{updates[0].Name}: 100%");
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingUpdatesOperationRunner : IOperationRunner
    {
        public List<UpdateEntry> CapturedUpdates { get; } = new();

        public Task RunApplyAsync(
            IReadOnlyList<AppEntry> apps,
            Action<string, UiStatusState> setStatusById,
            Action<string> appendOutput,
            Action<int, string> reportProgress,
            LocalizedStrings strings,
            Action<string, string, string>? setErrorById = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RunUpdatesAsync(
            IReadOnlyList<UpdateEntry> updates,
            Action<string, UiStatusState> setStatusById,
            Action<string> appendOutput,
            Action<int, string> reportProgress,
            LocalizedStrings strings,
            Action<string, string, string>? setErrorById = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            CapturedUpdates.AddRange(updates.Select(update => new UpdateEntry
            {
                Name = update.Name,
                Id = update.Id,
                Version = update.Version,
                Available = update.Available,
                Source = update.Source,
                Scope = update.Scope,
                Architecture = update.Architecture,
                Locale = update.Locale,
                InstallerType = update.InstallerType
            }));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDialogService : IDialogService
    {
        private readonly Queue<string> _promptResponses = new();
        private readonly Queue<PackageInterrogationDialogResult?> _interrogationResponses = new();
        public List<(string Message, string Title)> WarningCalls { get; } = new();
        public List<(string Message, string Title)> ErrorCalls { get; } = new();
        public List<(string Message, string Title)> InfoCalls { get; } = new();
        public List<PackageInterrogationRequest> PackageInterrogationRequests { get; } = new();
        public string? OpenFileResponse { get; set; }
        public string? SaveFileResponse { get; set; }
        public string? LastSaveFilePath { get; private set; }

        public bool Confirm(string message, string title) => false;

        public string Prompt(string prompt, string title, string defaultValue = "")
        {
            return _promptResponses.Count > 0 ? _promptResponses.Dequeue() : defaultValue;
        }

        public void EnqueuePrompt(string response)
        {
            _promptResponses.Enqueue(response);
        }

        public void EnqueueInterrogationResult(PackageInterrogationDialogResult result)
        {
            _interrogationResponses.Enqueue(result);
        }

        public void EnqueueInterrogationFailure(string message, string title)
        {
            _interrogationResponses.Enqueue(null);
            ErrorCalls.Add((message, title));
        }

        public void ShowError(string message, string title)
        {
            ErrorCalls.Add((message, title));
        }

        public void ShowInfo(string message, string title)
        {
            InfoCalls.Add((message, title));
        }

        public void ShowWarning(string message, string title)
        {
            WarningCalls.Add((message, title));
        }

        public string? OpenFile(string title, string filter, string defaultExtension = "json")
        {
            return OpenFileResponse;
        }

        public string? SaveFile(string title, string filter, string defaultFileName, string defaultExtension = "json")
        {
            LastSaveFilePath = SaveFileResponse ?? Path.Combine(Path.GetTempPath(), defaultFileName);
            return LastSaveFilePath;
        }

        public Task<PackageInterrogationDialogResult?> ShowPackageInterrogationAsync(PackageInterrogationRequest request, CancellationToken cancellationToken = default)
        {
            PackageInterrogationRequests.Add(request);
            return Task.FromResult(_interrogationResponses.Count > 0 ? _interrogationResponses.Dequeue() : null);
        }

        public Task<PackageInterrogationDialogResult?> ShowPackageInterrogationEditAsync(PackageInterrogationRequest request, AppEntry existingEntry, CancellationToken cancellationToken = default)
        {
            PackageInterrogationRequests.Add(request);
            return Task.FromResult(_interrogationResponses.Count > 0 ? _interrogationResponses.Dequeue() : null);
        }
    }

    private static PackageInterrogationDialogResult CreateInterrogationDialogResult(string id, string name, string version, bool reducedMode = false)
    {
        return new PackageInterrogationDialogResult
        {
            Interrogation = new PackageInterrogationResult
            {
                Success = true,
                IsReducedMode = reducedMode,
                Id = id,
                Name = name,
                Version = version,
                Source = "winget",
                InstallerType = "exe",
                DefaultSelection = new SelectedInstallOptions
                {
                    InstallMode = InstallModes.SilentWithProgress,
                    LogPath = "C:\\temp\\install.log"
                }
            },
            SelectedOptions = new SelectedInstallOptions
            {
                InstallMode = InstallModes.SilentWithProgress,
                LogPath = "C:\\temp\\install.log"
            }
        };
    }
}
