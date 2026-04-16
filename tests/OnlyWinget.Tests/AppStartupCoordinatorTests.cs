// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OnlyWinget.Models;
using OnlyWinget.Services;
using OnlyWinget.ViewModels;
using Xunit;

namespace OnlyWinget.Tests;

public sealed class AppStartupCoordinatorTests
{
    [Fact]
    public void CanContinueStartup_OpensAppInstallerPage_WhenWingetIsMissingAndUserConfirms()
    {
        var dialog = new RecordingDialogService();
        dialog.EnqueueConfirm(true);
        var wingetService = new WingetService(
            wingetRunner: static (singleArg, args, onOutputLine) => new WingetCommandResult
            {
                ExitCode = 1,
                Output = string.Empty
            });
        var viewModel = CreateViewModel(CreateTempDirectory(), wingetService, dialog);
        string? openedUrl = null;
        var coordinator = new AppStartupCoordinator(wingetService, dialog, url => openedUrl = url);

        var canContinue = coordinator.CanContinueStartup(viewModel);

        Assert.False(canContinue);
        Assert.Equal(AppStartupCoordinator.AppInstallerDownloadUrl, openedUrl);
        Assert.Single(dialog.ConfirmCalls);
        Assert.Contains(viewModel.Strings.WingetInstallPromptText, dialog.ConfirmCalls[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunPostStartupChecksAsync_ShowsSuccessAndCleansRuntime_WhenWingetUpdateSucceeds()
    {
        var root = CreateTempDirectory();
        var runtimeRoot = Path.Combine(root, "runtime");
        Directory.CreateDirectory(runtimeRoot);
        File.WriteAllText(Path.Combine(runtimeRoot, "temp.log"), "temp");

        try
        {
            var dialog = new RecordingDialogService();
            dialog.EnqueueConfirm(true);
            var upgraded = false;
            var wingetService = new WingetService(
                wingetRunner: (singleArg, args, onOutputLine) =>
                {
                    var command = singleArg ?? args[0];

                    if (command == "--version")
                    {
                        return new WingetCommandResult
                        {
                            ExitCode = 0,
                            Output = upgraded ? "v1.28.190" : "v1.12.470"
                        };
                    }

                    if (command == "upgrade" && args.Contains("--include-unknown"))
                    {
                        return new WingetCommandResult
                        {
                            ExitCode = 0,
                            Output = """
Name             Id                      Version   Available Source
-------------------------------------------------------------------
App Installer    Microsoft.AppInstaller  1.12.470  1.28.190  winget
"""
                        };
                    }

                    if (command == "upgrade")
                    {
                        upgraded = true;
                        return new WingetCommandResult
                        {
                            ExitCode = 0,
                            Output = "Upgrade completed successfully."
                        };
                    }

                    return new WingetCommandResult { ExitCode = 0, Output = string.Empty };
                },
                localRuntimeRoot: runtimeRoot);
            var viewModel = CreateViewModel(root, wingetService, dialog);
            var coordinator = new AppStartupCoordinator(wingetService, dialog);

            await coordinator.RunPostStartupChecksAsync(viewModel);

            Assert.Single(dialog.InfoCalls);
            Assert.Empty(dialog.WarningCalls);
            Assert.Equal(viewModel.Strings.WingetUpdateSuccessText, dialog.InfoCalls[0].Message);
            Assert.False(viewModel.IsWingetUpdateInProgress);
            Assert.Equal(string.Empty, viewModel.StatusText);
            Assert.Contains("Microsoft.AppInstaller", viewModel.OutputText, StringComparison.OrdinalIgnoreCase);
            // Runtime directory is preserved (only old logs are pruned, not the directory itself).
            Assert.True(Directory.Exists(runtimeRoot));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RunPostStartupChecksAsync_ShowsWarningAndResetsFlags_WhenWingetUpdateFails()
    {
        var root = CreateTempDirectory();
        var runtimeRoot = Path.Combine(root, "runtime");
        Directory.CreateDirectory(runtimeRoot);
        File.WriteAllText(Path.Combine(runtimeRoot, "temp.log"), "temp");

        try
        {
            var dialog = new RecordingDialogService();
            dialog.EnqueueConfirm(true);
            var wingetService = new WingetService(
                wingetRunner: static (singleArg, args, onOutputLine) =>
                {
                    var command = singleArg ?? args[0];

                    if (command == "--version")
                    {
                        return new WingetCommandResult
                        {
                            ExitCode = 0,
                            Output = "v1.12.470"
                        };
                    }

                    if (command == "upgrade" && args.Contains("--include-unknown"))
                    {
                        return new WingetCommandResult
                        {
                            ExitCode = 0,
                            Output = """
Name             Id                      Version   Available Source
-------------------------------------------------------------------
App Installer    Microsoft.AppInstaller  1.12.470  1.28.190  winget
"""
                        };
                    }

                    if (command == "upgrade")
                    {
                        return new WingetCommandResult
                        {
                            ExitCode = -1978335224,
                            Output = "Download failed."
                        };
                    }

                    return new WingetCommandResult { ExitCode = 0, Output = string.Empty };
                },
                localRuntimeRoot: runtimeRoot);
            var viewModel = CreateViewModel(root, wingetService, dialog);
            var coordinator = new AppStartupCoordinator(wingetService, dialog);

            await coordinator.RunPostStartupChecksAsync(viewModel);

            Assert.Empty(dialog.InfoCalls);
            Assert.Single(dialog.WarningCalls);
            Assert.Contains("Download failed", dialog.WarningCalls[0].Message, StringComparison.Ordinal);
            Assert.False(viewModel.IsWingetUpdateInProgress);
            Assert.Equal(string.Empty, viewModel.StatusText);
            Assert.Contains("Microsoft.AppInstaller", viewModel.OutputText, StringComparison.OrdinalIgnoreCase);
            // Runtime directory is preserved (only old logs are pruned, not the directory itself).
            Assert.True(Directory.Exists(runtimeRoot));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static MainViewModel CreateViewModel(string root, WingetService wingetService, RecordingDialogService dialogService)
    {
        var dataService = new AppDataService(appDataRoot: root, appBaseDirectory: root);
        var queryService = new WingetQueryService(wingetService);
        var operationRunner = new OperationRunner(wingetService, new InstallCommandBuilder(wingetService));
        var localizationService = new LocalizationService(
            new AppPreferencesService(root),
            () => CultureInfo.GetCultureInfo("en-US"));
        return new MainViewModel(
            queryService,
            new PresetWorkspaceService(dataService),
            localizationService,
            dialogService,
            new AppEntryService(wingetService),
            new TabService(),
            operationRunner,
            new UpdatesWorkspaceService(queryService, operationRunner));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "OnlyWinget.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class RecordingDialogService : IDialogService
    {
        private readonly Queue<bool> _confirmResponses = new();

        public List<(string Message, string Title)> ConfirmCalls { get; } = new();
        public List<(string Message, string Title)> InfoCalls { get; } = new();
        public List<(string Message, string Title)> WarningCalls { get; } = new();

        public void EnqueueConfirm(bool value)
        {
            _confirmResponses.Enqueue(value);
        }

        public bool Confirm(string message, string title)
        {
            ConfirmCalls.Add((message, title));
            return _confirmResponses.Count > 0 && _confirmResponses.Dequeue();
        }

        public string Prompt(string prompt, string title, string defaultValue = "")
        {
            return defaultValue;
        }

        public string? OpenFile(string title, string filter, string defaultExtension = "json")
        {
            return null;
        }

        public string? SaveFile(string title, string filter, string defaultFileName, string defaultExtension = "json")
        {
            return null;
        }

        public Task<PackageInterrogationDialogResult?> ShowPackageInterrogationAsync(PackageInterrogationRequest request)
        {
            return Task.FromResult<PackageInterrogationDialogResult?>(null);
        }

        public Task<PackageInterrogationDialogResult?> ShowPackageInterrogationEditAsync(PackageInterrogationRequest request, AppEntry existingEntry)
        {
            return Task.FromResult<PackageInterrogationDialogResult?>(null);
        }

        public void ShowError(string message, string title)
        {
        }

        public void ShowInfo(string message, string title)
        {
            InfoCalls.Add((message, title));
        }

        public void ShowWarning(string message, string title)
        {
            WarningCalls.Add((message, title));
        }
    }
}
