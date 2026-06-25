// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using OnlyWinget.Models;
using OnlyWinget.Services;
using Xunit;

namespace OnlyWinget.Tests;

public sealed class WingetCommandServiceAndOperationRunnerTests : IDisposable
{
    private readonly List<string> _temporaryPaths = new();

    [Fact]
    public async Task CheckForWingetUpdateAsync_UsesWingetUpgradeForAppInstaller()
    {
        var service = CreateWingetCommandService(
            wingetRunner: static (singleArg, args, onOutputLine) =>
            {
                var command = singleArg ?? args[0];
                if (command == "--version")
                {
                    return new WingetCommandResult { ExitCode = 0, Output = "v1.12.470" };
                }

                return new WingetCommandResult
                {
                    ExitCode = 0,
                    Output = """
Name             Id                      Version   Available Source
-------------------------------------------------------------------
App Installer    Microsoft.AppInstaller  1.12.470  1.28.190  winget
"""
                };
            });

        var result = await service.CheckForWingetUpdateAsync();

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal("1.12.470", result.InstalledVersion);
        Assert.Equal("1.28.190", result.LatestVersion);
    }

    [Fact]
    public async Task CheckForWingetUpdateAsync_DoesNotReportUpdate_WhenWingetSaysNoUpgrade()
    {
        var service = CreateWingetCommandService(
            wingetRunner: static (singleArg, args, onOutputLine) =>
            {
                var command = singleArg ?? args[0];
                if (command == "--version")
                {
                    return new WingetCommandResult { ExitCode = 0, Output = "v1.12.470" };
                }

                return new WingetCommandResult
                {
                    ExitCode = -1978335189,
                    Output = "Nessun aggiornamento disponibile."
                };
            });

        var result = await service.CheckForWingetUpdateAsync();

        Assert.False(result.IsUpdateAvailable);
        Assert.Equal("1.12.470", result.InstalledVersion);
        Assert.Equal(string.Empty, result.LatestVersion);
    }

    [Fact]
    public void UpdateSources_InvokesWingetSourceUpdate()
    {
        IReadOnlyList<string> invokedArgs = Array.Empty<string>();
        var service = CreateWingetCommandService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                invokedArgs = args;
                return new WingetCommandResult { ExitCode = 0, Output = "Done" };
            });

        var result = service.UpdateSources();

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(new[] { "source", "update" }, invokedArgs);
    }

    [Fact]
    public void UpgradeApp_RetriesByName_WhenExactIdDoesNotMatchInstalledPackage()
    {
        var invocations = new List<IReadOnlyList<string>>();
        var service = CreateWingetCommandService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                invocations.Add(args.ToArray());
                if (args.Contains("--id"))
                {
                    return new WingetCommandResult
                    {
                        ExitCode = -1978335212,
                        Output = "No installed package found matching input criteria."
                    };
                }

                return new WingetCommandResult { ExitCode = 0, Output = "Successfully installed" };
            });

        var result = service.UpgradeApp("Python.Python.3.14", "winget", "Python 3.14.4", "3.14.5rc1");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(2, invocations.Count);
        Assert.Contains("--id", invocations[0]);
        Assert.Contains("--name", invocations[1]);
        Assert.Contains("Python 3.14.4", invocations[1]);
        Assert.Contains("retrying with installed package name", result.Output);
    }

    [Fact]
    public void UpgradeApp_ReturnsAppInUse_WhenInstallerLogReportsMsixPackageInUse()
    {
        var service = CreateWingetCommandService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                var logIndex = args.ToList().IndexOf("--log");
                Assert.True(logIndex >= 0 && logIndex + 1 < args.Count);
                File.WriteAllText(
                    args[logIndex + 1],
                    "Deployment operation #1: Error 0x80073D02: Unable to install because the following apps need to be closed: Claude_1.5354.0.0_x64__pzs8sxrjxfjjc.");

                return new WingetCommandResult
                {
                    ExitCode = 0,
                    Output = "Installation completed. Restart the application to complete the update."
                };
            });

        var result = service.UpgradeApp("Anthropic.Claude", "winget", "Claude", "1.6608.1");

        Assert.Equal(-1978334975, result.ExitCode);
        Assert.Contains("Installer log:", result.Output, StringComparison.Ordinal);
        Assert.Contains("0x80073D02", result.Output, StringComparison.Ordinal);
        Assert.Contains("Claude_1.5354.0.0_x64__pzs8sxrjxfjjc", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Classifier_MapsPackagedServiceAdminRequirementToElevationHint()
    {
        var classifier = new WingetOutputClassifier();

        Assert.Equal("Administrator privileges required", classifier.GetErrorMessage(-2147009240, "en-US"));
        Assert.Equal("Re-run OnlyWinget as Administrator.", classifier.GetResolutionHint(-2147009240, "en-US"));
    }

    [Fact]
    public void Classifier_MapsShellExecuteInstallFailureToActionableHint()
    {
        var classifier = new WingetOutputClassifier();

        Assert.Equal("ShellExecute failed", classifier.GetErrorMessage(unchecked((int)0x8A150006), "en-US"));
        Assert.Contains("Administrator", classifier.GetResolutionHint(unchecked((int)0x8A150006), "en-US"), StringComparison.Ordinal);
        Assert.Contains("Amministratore", classifier.GetResolutionHint(unchecked((int)0x8A150006), "it-IT"), StringComparison.Ordinal);
    }

    [Fact]
    public void Classifier_MapsPackageMatchingAndMsixRollbackErrors()
    {
        var classifier = new WingetOutputClassifier();

        Assert.Equal("App not found", classifier.GetErrorMessage(-1978335212, "en-US"));
        Assert.Contains("retry using the installed package name", classifier.GetResolutionHint(-1978335212, "en-US"), StringComparison.Ordinal);
        Assert.Equal("MSIX package not found", classifier.GetErrorMessage(-2147009295, "en-US"));
        Assert.Equal("The MSIX package is not installed for the current user.", classifier.GetResolutionHint(-2147009295, "en-US"));
        Assert.Equal("Manifest not found", classifier.GetErrorMessage(-1978335209, "en-US"));
        Assert.Contains("preset version", classifier.GetResolutionHint(-1978335209, "en-US"), StringComparison.Ordinal);
        Assert.True(classifier.IsManifestNotFound(new WingetCommandResult
        {
            ExitCode = -1978335209,
            Output = "No version found matching: 9.7.1"
        }));
    }

    [Fact]
    public void Classifier_MapsOfficialWinGetCatalogFallbacks()
    {
        var classifier = new WingetOutputClassifier();

        Assert.True(WingetKnownErrorCatalog.Count >= 200);
        Assert.False(WingetKnownErrorCatalog.ContainsDuplicateCodes);
        Assert.Equal(
            "An upgrade is available but uses a different install technology than the current installation",
            classifier.GetErrorMessage(unchecked((int)0x8A15008E), "en-US"));
        Assert.Contains(
            "winget error 0x8A15008E",
            classifier.GetResolutionHint(unchecked((int)0x8A15008E), "en-US"),
            StringComparison.Ordinal);
        Assert.Equal(
            "Il file di configurazione non è valido.",
            classifier.GetErrorMessage(unchecked((int)0x8A15C001), "it-IT"));
        Assert.True(WingetKnownErrorCatalog.TryGetDescription(unchecked((int)0x8A150017), "it-IT", out var description));
        Assert.Equal("Nessun manifesto trovato corrispondente ai criteri", description);
    }

    [Fact]
    public void Classifier_MapsLocalTimeoutAndCancellationResults()
    {
        var classifier = new WingetOutputClassifier();

        Assert.Equal("Operation cancelled", classifier.GetErrorMessage(9997, "en-US"));
        Assert.Equal("Execution timeout", classifier.GetErrorMessage(9998, "en-US"));
        Assert.Equal("Operation cancelled.", classifier.GetResolutionHint(9997, "en-US"));
        Assert.Equal("The operation exceeded the maximum allowed time. Check the log and retry.", classifier.GetResolutionHint(9998, "en-US"));
    }

    [Fact]
    public void ElevatedWingetLauncher_BuildArgumentString_RoundTripsWindowsArguments()
    {
        var args = new[]
        {
            "install",
            "--id",
            "Contoso.Tool",
            "--custom",
            "/DIR=\"C:\\Program Files\\Contoso Tool\\\"",
            "--override",
            "PROPERTY=\"value with spaces\"",
            "--log",
            "C:\\Temp\\OnlyWinget Logs\\install.log",
            "--empty",
            string.Empty,
            "C:\\Path With Trailing Slash\\"
        };

        var parsed = ParseWindowsCommandLine("winget " + ElevatedWingetLauncher.BuildArgumentString(args));

        Assert.Equal(args, parsed.Skip(1));
    }

    [Fact]
    public void ElevatedWingetLauncher_ReadNewLogLines_EmitsOnlyNewLines()
    {
        var root = CreateTempDirectory();
        var logPath = Path.Combine(root, "install.log");
        var lines = new List<string>();
        File.WriteAllText(logPath, "25%" + Environment.NewLine);

        var position = ElevatedWingetLauncher.ReadNewLogLines(logPath, 0, lines.Add);
        File.AppendAllText(logPath, "50%" + Environment.NewLine);
        position = ElevatedWingetLauncher.ReadNewLogLines(logPath, position, lines.Add);
        ElevatedWingetLauncher.ReadNewLogLines(logPath, position, lines.Add);

        Assert.Equal(new[] { "25%", "50%" }, lines);
    }

    [Fact]
    public async Task RunApplyAsync_UsesInstallCommand_ForInstallAction()
    {
        var invokedCommands = new List<string>();
        var service = CreateWingetCommandService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                var command = singleArg ?? args[0];
                invokedCommands.Add(command);
                return new WingetCommandResult { ExitCode = 0, Output = "installed" };
            });
        var runner = new OperationRunner(service, new InstallCommandBuilder(service));
        var status = string.Empty;
        var strings = LocalizedStrings.English;

        await runner.RunApplyAsync(
            new[]
            {
                new AppEntry { Name = "VS Code", Id = "Microsoft.VisualStudioCode", Action = AppActions.Install }
            },
            (_, value) => status = RenderStatus(value, strings),
            _ => { },
            (_, _) => { },
            strings);

        Assert.Contains("show", invokedCommands);
        Assert.Single(invokedCommands, command => command == "install");
        Assert.Equal("OK", status);
    }

    [Fact]
    public async Task RunApplyAsync_StopsQueue_WhenCancellationIsRequested()
    {
        using var cancellation = new System.Threading.CancellationTokenSource();
        var invocations = 0;
        var output = new List<string>();
        var service = CreateWingetCommandService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                invocations++;
                cancellation.Cancel();
                return new WingetCommandResult { ExitCode = 0, Output = "installed" };
            });
        var runner = new OperationRunner(service, new InstallCommandBuilder(service));

        await runner.RunApplyAsync(
            new[]
            {
                new AppEntry { Name = "One", Id = "Contoso.One", Action = AppActions.Install },
                new AppEntry { Name = "Two", Id = "Contoso.Two", Action = AppActions.Install }
            },
            (_, _) => { },
            output.Add,
            (_, _) => { },
            LocalizedStrings.English,
            cancellationToken: cancellation.Token);

        Assert.Equal(1, invocations);
        Assert.Contains(output, line => line.Contains("event=apply_cancelled", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunApplyAsync_RedactsCustomAndOverrideArguments_InDiagnosticLog()
    {
        IReadOnlyList<string> invokedArgs = Array.Empty<string>();
        var output = new List<string>();
        var service = CreateWingetCommandService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                invokedArgs = args;
                return new WingetCommandResult { ExitCode = 0, Output = "installed" };
            });
        var runner = new OperationRunner(service, new InstallCommandBuilder(service));

        await runner.RunApplyAsync(
            new[]
            {
                new AppEntry
                {
                    Name = "Internal Tool",
                    Id = "Contoso.InternalTool",
                    Action = AppActions.Install,
                    OverrideArgs = "/token super-secret-token"
                }
            },
            (_, _) => { },
            output.Add,
            (_, _) => { },
            LocalizedStrings.English);

        Assert.Contains("--override", invokedArgs);
        Assert.Contains("/token super-secret-token", invokedArgs);
        var commandLog = Assert.Single(output, line => line.Contains("event=install_command_built", StringComparison.Ordinal));
        Assert.Contains("--override [redacted]", commandLog, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret-token", commandLog, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunApplyAsync_BlocksUnreviewedAdvancedArguments()
    {
        var invokedCommands = new List<string>();
        var output = new List<string>();
        var errorMessage = string.Empty;
        var resolution = string.Empty;
        var status = string.Empty;
        var strings = LocalizedStrings.English;
        var service = CreateWingetCommandService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                invokedCommands.Add(singleArg ?? args[0]);
                return new WingetCommandResult { ExitCode = 0, Output = "installed" };
            });
        var runner = new OperationRunner(service, new InstallCommandBuilder(service));

        await runner.RunApplyAsync(
            new[]
            {
                new AppEntry
                {
                    Name = "Internal Tool",
                    Id = "Contoso.InternalTool",
                    Action = AppActions.Install,
                    OverrideArgs = "/unsafe",
                    AdvancedArgumentsReviewed = false
                }
            },
            (_, value) => status = RenderStatus(value, strings),
            output.Add,
            (_, _) => { },
            strings,
            (_, message, hint) =>
            {
                errorMessage = message;
                resolution = hint;
            });

        Assert.Empty(invokedCommands);
        Assert.Equal(strings.AdvancedArgumentsReviewRequiredText, status);
        Assert.Equal(strings.AdvancedArgumentsReviewRequiredText, errorMessage);
        Assert.Equal(strings.AdvancedArgumentsReviewRequiredResolution, resolution);
        Assert.Contains(output, line => line.Contains("event=advanced_arguments_review_required", StringComparison.Ordinal));
        Assert.DoesNotContain(output, line => line.Contains("/unsafe", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunApplyAsync_SkipsWingetInvocation_ForPauseAction()
    {
        var invokedCommands = new List<string>();
        var reportedProgress = new List<int>();
        var service = CreateWingetCommandService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                invokedCommands.Add(singleArg ?? args[0]);
                return new WingetCommandResult { ExitCode = 0, Output = string.Empty };
            });
        var runner = new OperationRunner(service, new InstallCommandBuilder(service));
        var status = string.Empty;
        var strings = LocalizedStrings.English;

        await runner.RunApplyAsync(
            new[]
            {
                new AppEntry { Name = "VS Code", Id = "Microsoft.VisualStudioCode", Action = AppActions.Pause }
            },
            (_, value) => status = RenderStatus(value, strings),
            _ => { },
            (percentage, _) => reportedProgress.Add(percentage),
            strings);

        Assert.Empty(invokedCommands);
        Assert.Equal("Paused", status);
        Assert.Contains(100, reportedProgress);
    }

    [Fact]
    public async Task RunApplyAsync_ReportsProgress_FromLiveWingetOutput()
    {
        var reportedProgress = new List<int>();
        var service = CreateWingetCommandService(
            wingetRunner: static (singleArg, args, onOutputLine) =>
            {
                var command = singleArg ?? args[0];
                if (command == "install")
                {
                    onOutputLine?.Invoke("50%");
                    return new WingetCommandResult { ExitCode = 0, Output = "completed" };
                }

                return new WingetCommandResult { ExitCode = 0, Output = string.Empty };
            });
        var runner = new OperationRunner(service, new InstallCommandBuilder(service));

        await runner.RunApplyAsync(
            new[]
            {
                new AppEntry { Name = "VS Code", Id = "Microsoft.VisualStudioCode", Action = AppActions.Install }
            },
            (_, _) => { },
            _ => { },
            (percentage, _) => reportedProgress.Add(percentage),
            LocalizedStrings.English);

        Assert.Contains(0, reportedProgress);
        Assert.Contains(50, reportedProgress);
        Assert.Contains(100, reportedProgress);
    }

    [Fact]
    public async Task RunApplyAsync_ReportsProgress_FromElevatedWingetLogOutput()
    {
        var reportedProgress = new List<int>();
        var receivedLogPath = string.Empty;
        var service = CreateWingetCommandService(
            wingetRunner: static (singleArg, args, onOutputLine) => new WingetCommandResult { ExitCode = 0, Output = string.Empty });
        var elevatedLauncher = new FakeElevatedWingetLauncher((args, logPath, onOutputLine, cancellationToken) =>
        {
            receivedLogPath = logPath ?? string.Empty;
            onOutputLine?.Invoke("50%");
            return new WingetCommandResult { ExitCode = 0, Output = "event=elevated_launch_completed exit_code=0" };
        });
        var runner = new OperationRunner(service, new InstallCommandBuilder(service), elevatedLauncher, isCurrentProcessElevated: false);

        await runner.RunApplyAsync(
            new[]
            {
                new AppEntry
                {
                    Name = "Machine Tool",
                    Id = "Contoso.MachineTool",
                    Source = "winget",
                    Action = AppActions.Install,
                    Scope = "machine"
                }
            },
            (_, _) => { },
            _ => { },
            (percentage, _) => reportedProgress.Add(percentage),
            LocalizedStrings.English);

        Assert.False(string.IsNullOrWhiteSpace(receivedLogPath));
        Assert.Contains(-1, reportedProgress);
        Assert.Contains(50, reportedProgress);
        Assert.Contains(100, reportedProgress);
    }

    [Fact]
    public async Task RunApplyAsync_ReportsProgress_FromWingetDownloadSizes()
    {
        var reportedProgress = new List<int>();
        var status = string.Empty;
        var service = CreateWingetCommandService(
            wingetRunner: static (singleArg, args, onOutputLine) =>
            {
                onOutputLine?.Invoke("  ███████████████▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒  5.00 MB / 10.0 MB");
                return new WingetCommandResult { ExitCode = 0, Output = "completed" };
            });
        var runner = new OperationRunner(service, new InstallCommandBuilder(service));

        await runner.RunApplyAsync(
            new[]
            {
                new AppEntry { Name = "Google Play Games", Id = "Google.PlayGames", Action = AppActions.Install }
            },
            (_, value) => status = RenderStatus(value, LocalizedStrings.English),
            _ => { },
            (percentage, _) => reportedProgress.Add(percentage),
            LocalizedStrings.English);

        Assert.Contains(50, reportedProgress);
        Assert.Contains(100, reportedProgress);
        Assert.Equal("OK", status);
    }

    [Fact]
    public async Task RunApplyAsync_ReportsIndeterminateProgress_ForInstallerPhaseWithoutPercentage()
    {
        var reportedProgress = new List<int>();
        var reportedText = new List<string>();
        var service = CreateWingetCommandService(
            wingetRunner: static (singleArg, args, onOutputLine) =>
            {
                onOutputLine?.Invoke("Avvio installazione pacchetto in corso...");
                return new WingetCommandResult { ExitCode = 0, Output = "Installazione riuscita" };
            });
        var runner = new OperationRunner(service, new InstallCommandBuilder(service));

        await runner.RunApplyAsync(
            new[]
            {
                new AppEntry { Name = "Google Play Games", Id = "Google.PlayGames", Action = AppActions.Install }
            },
            (_, _) => { },
            _ => { },
            (percentage, text) =>
            {
                reportedProgress.Add(percentage);
                reportedText.Add(text);
            },
            LocalizedStrings.Italian);

        Assert.Contains(-1, reportedProgress);
        Assert.Contains(reportedText, text => text.Contains("Avvio installazione", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(100, reportedProgress);
    }

    [Fact]
    public void UiStatusFormatting_IsConsistentForPresetAndUpdateRows()
    {
        var appEntry = new AppEntry();
        var updateEntry = new UpdateEntry();

        appEntry.ApplyStatus(UiStatusState.FromKey(UiStatusKey.InstallInProgress, 55), LocalizedStrings.English);
        updateEntry.ApplyStatus(UiStatusState.FromKey(UiStatusKey.InstallInProgress, 55), LocalizedStrings.English);

        Assert.Equal("Installing... 55%", appEntry.Status);
        Assert.Equal(appEntry.Status, updateEntry.Status);

        appEntry.ApplyStatus(UiStatusState.FromRawText("custom status"), LocalizedStrings.Italian);
        updateEntry.ApplyStatus(UiStatusState.FromRawText("custom status"), LocalizedStrings.Italian);

        Assert.Equal("custom status", appEntry.Status);
        Assert.Equal(appEntry.Status, updateEntry.Status);
    }

    [Fact]
    public void Search_ParsesWingetTableOutput_AfterBanner()
    {
        const string output = """
Found 2 packages.
Name                         Id                               Version
----------------------------------------------------------------------
Visual Studio Code           Microsoft.VisualStudioCode       1.100.0
Windows Terminal             Microsoft.WindowsTerminal        1.22.10352.0
""";
        var service = CreateWingetCommandService(
            wingetRunner: static (singleArg, args, onOutputLine) => new WingetCommandResult { ExitCode = 0, Output = output });

        var results = new WingetQueryService(service).Search("code");

        Assert.Collection(
            results,
            first =>
            {
                Assert.Equal("Visual Studio Code", first.Name);
                Assert.Equal("Microsoft.VisualStudioCode", first.Id);
                Assert.Equal("1.100.0", first.Version);
            },
            second => Assert.Equal("Microsoft.WindowsTerminal", second.Id));
    }

    [Fact]
    public void Search_PreservesUnicodePackageNames()
    {
        const string output = """
Found 2 packages.
Name                         Id                               Version
----------------------------------------------------------------------
Café Déjà Vu                 Contoso.CafeDejaVu               1.2.3
Über Tool                    Contoso.UberTool                 4.5.6
""";
        var service = CreateWingetCommandService(
            wingetRunner: static (singleArg, args, onOutputLine) => new WingetCommandResult { ExitCode = 0, Output = output });

        var results = new WingetQueryService(service).Search("unicode");

        Assert.Collection(
            results,
            first =>
            {
                Assert.Equal("Café Déjà Vu", first.Name);
                Assert.Equal("Contoso.CafeDejaVu", first.Id);
                Assert.DoesNotContain("Ã", first.Name, StringComparison.Ordinal);
            },
            second =>
            {
                Assert.Equal("Über Tool", second.Name);
                Assert.Equal("Contoso.UberTool", second.Id);
                Assert.DoesNotContain("Ã", second.Name, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void Search_DoesNotRestrictResultsToWingetSource()
    {
        IReadOnlyList<string> invokedArgs = Array.Empty<string>();
        var service = CreateWingetCommandService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                invokedArgs = args;
                return new WingetCommandResult
                {
                    ExitCode = 0,
                    Output = """
Name             Id                    Version Source
-----------------------------------------------------
Windows Camera   9WZDNCRFJBBG          Unknown msstore
"""
                };
            });

        var results = new WingetQueryService(service).Search("camera");

        Assert.DoesNotContain("--source", invokedArgs);
        var result = Assert.Single(results);
        Assert.Equal("9WZDNCRFJBBG", result.Id);
        Assert.Equal("msstore", result.Source);
    }

    [Fact]
    public void Search_ExpandsTruncatedRows_WithTargetedIdLookups()
    {
        var service = CreateWingetCommandService(
            wingetRunner: static (singleArg, args, onOutputLine) =>
            {
                var command = singleArg ?? args[0];
                if (command != "search")
                {
                    return new WingetCommandResult { ExitCode = 0, Output = string.Empty };
                }

                if (args.Contains("--query"))
                {
                    return new WingetCommandResult
                    {
                        ExitCode = 0,
                        Output = """
Name                                  Id                                    Version                    Match    Source
-----------------------------------------------------------------------------------------------------------------------
Microsoft .NET Windows Desktop Runti… Microsoft.DotNet.DesktopRuntime.10    10.0.6                     Tag: net winget
Microsoft Windows Desktop Runtime - … Microsoft.DotNet.DesktopRuntime.8.ar… 8.0.25                     Tag: net winget
"""
                    };
                }

                var idIndex = Array.IndexOf(args.ToArray(), "--id");
                if (idIndex < 0 || idIndex + 1 >= args.Count)
                {
                    return new WingetCommandResult { ExitCode = 1, Output = string.Empty };
                }

                return args[idIndex + 1] switch
                {
                    "Microsoft.DotNet.DesktopRuntime.10" => new WingetCommandResult
                    {
                        ExitCode = 0,
                        Output = """
Name                                       Id                                   Version Source
------------------------------------------------------------------------------------------------
Microsoft .NET Windows Desktop Runtime 10.0 Microsoft.DotNet.DesktopRuntime.10   10.0.6  winget
"""
                    },
                    "Microsoft.DotNet.DesktopRuntime.8.ar" => new WingetCommandResult
                    {
                        ExitCode = 0,
                        Output = """
Name                                               Id                                      Version Source
---------------------------------------------------------------------------------------------------------
Microsoft Windows Desktop Runtime - 8.0.25 (arm64) Microsoft.DotNet.DesktopRuntime.8.arm64 8.0.25  winget
"""
                    },
                    _ => new WingetCommandResult { ExitCode = 1, Output = string.Empty }
                };
            });

        var results = new WingetQueryService(service).Search("net");

        Assert.Collection(
            results,
            first =>
            {
                Assert.Equal("Microsoft .NET Windows Desktop Runtime 10.0", first.Name);
                Assert.Equal("Microsoft.DotNet.DesktopRuntime.10", first.Id);
                Assert.Equal("10.0.6", first.Version);
            },
            second =>
            {
                Assert.Equal("Microsoft Windows Desktop Runtime - 8.0.25 (arm64)", second.Name);
                Assert.Equal("Microsoft.DotNet.DesktopRuntime.8.arm64", second.Id);
                Assert.Equal("8.0.25", second.Version);
            });
    }

    [Fact]
    public void AppEntryValidation_UsesRequestedSource_WhenCheckingPackageExists()
    {
        IReadOnlyList<string> invokedArgs = Array.Empty<string>();
        var wingetService = CreateWingetCommandService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                invokedArgs = args;
                return new WingetCommandResult { ExitCode = 0, Output = "found" };
            });
        var service = new AppEntryService(new WingetQueryService(wingetService));

        var validation = service.ValidateForInsert("9WZDNCRFJBBG", Array.Empty<AppEntry>(), "msstore");

        Assert.Equal(AppEntryValidationError.None, validation);
        Assert.Contains("--source", invokedArgs);
        Assert.Contains("msstore", invokedArgs);
    }

    [Fact]
    public void AppEntryValidation_AllowsSameIdAndArchitecture_WhenSourceDiffers()
    {
        var wingetService = CreateWingetCommandService(
            wingetRunner: static (singleArg, args, onOutputLine) => new WingetCommandResult { ExitCode = 0, Output = "found" });
        var service = new AppEntryService(new WingetQueryService(wingetService));
        var currentApps = new[]
        {
            new AppEntry { Id = "Contoso.Tool", Source = "winget", Architecture = "x64" }
        };

        var validation = service.ValidateForInsert("Contoso.Tool", currentApps, "msstore", "x64");

        Assert.Equal(AppEntryValidationError.None, validation);
    }

    [Fact]
    public void AppEntryValidation_BlocksSameIdSourceAndArchitecture()
    {
        var wingetService = CreateWingetCommandService(
            wingetRunner: static (singleArg, args, onOutputLine) => new WingetCommandResult { ExitCode = 0, Output = "found" });
        var service = new AppEntryService(new WingetQueryService(wingetService));
        var currentApps = new[]
        {
            new AppEntry { Id = "Contoso.Tool", Source = "msstore", Architecture = "x64" }
        };

        var validation = service.ValidateForInsert("Contoso.Tool", currentApps, "msstore", "x64");

        Assert.Equal(AppEntryValidationError.DuplicateId, validation);
    }

    [Fact]
    public void LoadUpdates_ParsesLocalizedOutput_WithNoiseAndProgressLines()
    {
        const string output = """
Ricerca aggiornamenti disponibili...
-
Nome                 ID                         Versione Disponibile Source
---------------------------------------------------------------------------
App Installer        Microsoft.AppInstaller     1.12.470 1.28.190    winget
Microsoft PowerToys  Microsoft.PowerToys        0.90.0   0.90.1      msstore
""";
        var service = CreateWingetCommandService(
            wingetRunner: static (singleArg, args, onOutputLine) => new WingetCommandResult { ExitCode = 0, Output = output });

        var updates = new WingetQueryService(service).LoadUpdates();

        Assert.Equal(2, updates.Count);
        Assert.Contains(updates, entry => entry.Id == "Microsoft.AppInstaller" && entry.Available == "1.28.190");
        Assert.Contains(updates, entry => entry.Id == "Microsoft.PowerToys" && entry.Version == "0.90.0" && entry.Source == "msstore");
    }

    [Fact]
    public void LoadUpdates_IgnoresTrailingSummaryLine()
    {
        const string output = """
Nome                      Id                Versione       Disponibile   Origine
--------------------------------------------------------------------------------
Adobe Acrobat DC (64-bit) Adobe.Acrobat.Pro 22.001.20085   25.001.21223  winget
Microsoft Edge            Microsoft.Edge    146.0.3856.109 147.0.3912.60 winget
CapCut                    ByteDance.CapCut  8.3.0.3497     8.4.0.3562    winget
3 aggiornamenti disponibili.
""";
        var service = CreateWingetCommandService(
            wingetRunner: static (singleArg, args, onOutputLine) => new WingetCommandResult { ExitCode = 0, Output = output });

        var updates = new WingetQueryService(service).LoadUpdates();

        Assert.Equal(3, updates.Count);
        Assert.DoesNotContain(updates, entry => string.Equals(entry.Id, "i.", StringComparison.Ordinal));
        Assert.Contains(updates, entry => entry.Id == "Adobe.Acrobat.Pro");
        Assert.Contains(updates, entry => entry.Id == "Microsoft.Edge");
        Assert.Contains(updates, entry => entry.Id == "ByteDance.CapCut");
    }

    [Fact]
    public void LoadUpdates_PreservesGooglePlayGamesVersionTokens()
    {
        const string output = """
Nome              Id               Versione  Disponibile  Origine
-----------------------------------------------------------------
Google Play Games Google.PlayGames 26.5.27.1 149.0.7814.0 winget
""";
        var service = CreateWingetCommandService(
            wingetRunner: (singleArg, args, onOutputLine) => new WingetCommandResult { ExitCode = 0, Output = output });

        var update = Assert.Single(new WingetQueryService(service).LoadUpdates());

        Assert.Equal("Google Play Games", update.Name);
        Assert.Equal("Google.PlayGames", update.Id);
        Assert.Equal("26.5.27.1", update.Version);
        Assert.Equal("149.0.7814.0", update.Available);
    }

    [Fact]
    public void LoadUpdates_PreservesAdvertisedVersion_WhenLocalizedStatusColumnsReportNoUpdate()
    {
        var output = string.Join(
            Environment.NewLine,
            string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0,-25}{1,-18}{2,-14}{3,-13}{4,-20}{5}", "Nome", "Id", "Versione", "Disponibile", "Origine", "Stato"),
            new string('-', 110),
            string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0,-25}{1,-18}{2,-14}{3,-13}{4,-20}{5}", "AxCrypt 2.1.1693.0", "AxCrypt.AxCrypt", "2.1.1693.0", "3.0.94", "Nessun aggiornamento", "Gia alla versione piu recente."));
        var service = CreateWingetCommandService(
            wingetRunner: (singleArg, args, onOutputLine) => new WingetCommandResult { ExitCode = 0, Output = output });

        var update = Assert.Single(new WingetQueryService(service).LoadUpdates());

        Assert.Equal("AxCrypt.AxCrypt", update.Id);
        Assert.Equal("2.1.1693.0", update.Version);
        Assert.Equal("3.0.94", update.Available);
        Assert.Equal("winget", update.Source);
    }


    [Fact]
    public async Task RunUpdatesAsync_UsesUpdateSource_WhenInvokingUpgrade()
    {
        IReadOnlyList<string> invokedArgs = Array.Empty<string>();
        var service = CreateWingetCommandService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                var command = singleArg ?? args[0];
                if (command == "upgrade")
                {
                    invokedArgs = args;
                }

                return new WingetCommandResult { ExitCode = 0, Output = "updated" };
            });
        var runner = new OperationRunner(service, new InstallCommandBuilder(service));

        await runner.RunUpdatesAsync(
            new[]
            {
                new UpdateEntry
                {
                    Name = "Windows Camera",
                    Id = "9WZDNCRFJBBG",
                    Source = "msstore",
                    IsSelected = true
                }
            },
            (_, _) => { },
            _ => { },
            (_, _) => { },
            LocalizedStrings.English);

        Assert.Contains("upgrade", invokedArgs);
        Assert.Contains("--source", invokedArgs);
        Assert.Contains("msstore", invokedArgs);
        Assert.Contains("--include-pinned", invokedArgs);
    }

    [Fact]
    public async Task RunUpdatesAsync_VerifiesSuccessfulUpgradeAndReportsStillAvailable_ForAnyPackage()
    {
        var commands = new List<IReadOnlyList<string>>();
        var output = new List<string>();
        var status = string.Empty;
        var error = string.Empty;
        var resolution = string.Empty;
        var service = CreateWingetCommandService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                commands.Add(args.ToArray());
                var command = singleArg ?? args[0];
                return command switch
                {
                    "upgrade" => new WingetCommandResult { ExitCode = 0, Output = "Successfully installed" },
                    "list" => new WingetCommandResult
                    {
                        ExitCode = 0,
                        Output = """
Nome              Id               Versione  Disponibile  Origine
-----------------------------------------------------------------
Google Play Games Google.PlayGames 26.5.27.1 149.0.7814.0 winget
"""
                    },
                    _ => new WingetCommandResult { ExitCode = 0, Output = string.Empty }
                };
            });
        var runner = new OperationRunner(service, new InstallCommandBuilder(service));

        await runner.RunUpdatesAsync(
            new[]
            {
                new UpdateEntry
                {
                    Name = "Google Play Games",
                    Id = "Google.PlayGames",
                    Version = "26.5.27.1",
                    Available = "149.0.7814.0",
                    Source = "winget",
                    IsSelected = true
                }
            },
            (_, value) => status = RenderStatus(value, LocalizedStrings.Italian),
            output.Add,
            (_, _) => { },
            LocalizedStrings.Italian,
            (_, errorMessage, resolutionHint) =>
            {
                error = errorMessage;
                resolution = resolutionHint;
            });

        Assert.Contains(commands, args => string.Equals(args[0], "upgrade", StringComparison.Ordinal));
        Assert.Contains(commands, args => string.Equals(args[0], "list", StringComparison.Ordinal) && args.Contains("--upgrade-available"));
        Assert.Equal("Aggiornamento ancora disponibile", status);
        Assert.Equal("Aggiornamento ancora disponibile", error);
        Assert.Contains("26.5.27.1 -> 149.0.7814.0", resolution, StringComparison.Ordinal);
        Assert.Contains("senza cambiare la versione installata registrata", resolution, StringComparison.Ordinal);
        Assert.Contains(output, line => line.Contains("event=update_still_available id=\"Google.PlayGames\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunUpdatesAsync_RetriesNoApplicableUpgrade_WithInstalledScopeArchitectureAndInstallerLocale()
    {
        var upgradeInvocations = new List<IReadOnlyList<string>>();
        var service = CreateWingetCommandService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                var command = singleArg ?? args[0];
                if (command == "upgrade")
                {
                    upgradeInvocations.Add(args.ToArray());
                    if (args.Contains("--locale"))
                    {
                        return new WingetCommandResult { ExitCode = 0, Output = "Successfully installed" };
                    }

                    return new WingetCommandResult
                    {
                        ExitCode = -1978335189,
                        Output = """
No applicable upgrade found.
A newer package version is available in a configured source, but it does not apply to your system or requirements.
"""
                    };
                }

                if (command == "list")
                {
                    return new WingetCommandResult
                    {
                        ExitCode = 0,
                        Output = """
WinRAR 7.20 (64-bit) [RARLab.WinRAR]
Installed Scope: Machine
Installed Architecture: X64
Installed Locale: it-IT
"""
                    };
                }

                if (command == "show")
                {
                    Assert.Contains("--version", args);
                    Assert.Contains("7.22.0", args);
                    return new WingetCommandResult
                    {
                        ExitCode = 0,
                        Output = """
Found WinRAR [RARLab.WinRAR]
Version: 7.22.0
Installer:
  Installer Locale: en
"""
                    };
                }

                return new WingetCommandResult { ExitCode = 0, Output = string.Empty };
            });
        var runner = new OperationRunner(service, new InstallCommandBuilder(service));
        var status = string.Empty;

        await runner.RunUpdatesAsync(
            new[]
            {
                new UpdateEntry
                {
                    Name = "WinRAR",
                    Id = "RARLab.WinRAR",
                    Version = "7.20.0",
                    Available = "7.22.0",
                    Source = "winget",
                    IsSelected = true
                }
            },
            (_, value) => status = RenderStatus(value, LocalizedStrings.English),
            _ => { },
            (_, _) => { },
            LocalizedStrings.English);

        Assert.Equal("OK", status);
        Assert.Equal(2, upgradeInvocations.Count);
        var retryArgs = upgradeInvocations[1];
        Assert.Contains("--scope", retryArgs);
        Assert.Contains("machine", retryArgs);
        Assert.Contains("--architecture", retryArgs);
        Assert.Contains("x64", retryArgs);
        Assert.Contains("--locale", retryArgs);
        Assert.Contains("en", retryArgs);
        Assert.Contains("--include-pinned", retryArgs);
    }

    [Fact]
    public async Task RunUpdatesAsync_RetriesNoApplicableUpgrade_WithConfiguredPackageOptions()
    {
        var upgradeInvocations = new List<IReadOnlyList<string>>();
        var resolution = string.Empty;
        var service = CreateWingetCommandService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                var command = singleArg ?? args[0];
                if (command == "upgrade")
                {
                    upgradeInvocations.Add(args.ToArray());
                    return new WingetCommandResult
                    {
                        ExitCode = -1978335189,
                        Output = """
No applicable upgrade found.
A newer package version is available in a configured source, but it does not apply to your system or requirements.
"""
                    };
                }

                if (command == "list")
                {
                    return new WingetCommandResult
                    {
                        ExitCode = 0,
                        Output = """
WinRAR 7.20 (64-bit) [RARLab.WinRAR]
Installed Scope: Machine
Installed Architecture: X64
Installed Locale: it-IT
"""
                    };
                }

                if (command == "show")
                {
                    return new WingetCommandResult
                    {
                        ExitCode = 0,
                        Output = """
Found WinRAR [RARLab.WinRAR]
Version: 7.22.0
Installer:
  Installer Locale: en
"""
                    };
                }

                return new WingetCommandResult { ExitCode = 0, Output = string.Empty };
            });
        var runner = new OperationRunner(service, new InstallCommandBuilder(service));

        await runner.RunUpdatesAsync(
            new[]
            {
                new UpdateEntry
                {
                    Name = "WinRAR",
                    Id = "RARLab.WinRAR",
                    Version = "7.20.0",
                    Available = "7.22.0",
                    Source = "winget",
                    Scope = "machine",
                    Architecture = "x64",
                    Locale = "it",
                    InstallerType = "exe",
                    IsSelected = true
                }
            },
            (_, _) => { },
            _ => { },
            (_, _) => { },
            LocalizedStrings.Italian,
            (_, _, resolutionHint) => resolution = resolutionHint);

        Assert.Equal(2, upgradeInvocations.Count);
        var retryArgs = upgradeInvocations[1];
        Assert.Contains("--scope", retryArgs);
        Assert.Contains("machine", retryArgs);
        Assert.Contains("--architecture", retryArgs);
        Assert.Contains("x64", retryArgs);
        Assert.Contains("--locale", retryArgs);
        Assert.Contains("it", retryArgs);
        Assert.Contains("--installer-type", retryArgs);
        Assert.Contains("exe", retryArgs);
        Assert.Contains("locale=it", resolution, StringComparison.Ordinal);
        Assert.Contains("Modifica le opzioni del pacchetto", resolution, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunUpdatesAsync_LogsNoopResult_WhenWingetReportsNoUpgrade()
    {
        var output = new List<string>();
        var error = string.Empty;
        var resolution = string.Empty;
        var service = CreateWingetCommandService(
            wingetRunner: static (singleArg, args, onOutputLine) => new WingetCommandResult
            {
                ExitCode = -1978335189,
                Output = "No available upgrade found."
            });
        var runner = new OperationRunner(service, new InstallCommandBuilder(service));

        await runner.RunUpdatesAsync(
            new[]
            {
                new UpdateEntry
                {
                    Name = "WinRAR",
                    Id = "RARLab.WinRAR",
                    Source = "winget",
                    IsSelected = true
                }
            },
            (_, _) => { },
            output.Add,
            (_, _) => { },
            LocalizedStrings.English,
            (_, errorMessage, resolutionHint) =>
            {
                error = errorMessage;
                resolution = resolutionHint;
            });

        Assert.Equal("No update available", error);
        Assert.Equal("Already at the latest version.", resolution);
        Assert.Contains(output, line => line.Contains("event=winget_upgrade_noop", StringComparison.Ordinal));
        Assert.Contains(output, line => line.Contains("exit_code=-1978335189", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunUpdatesAsync_ReportsAdvertisedUpdateNotApplied_WhenWingetNoopsDespiteAvailableVersion()
    {
        var output = new List<string>();
        var status = string.Empty;
        var error = string.Empty;
        var resolution = string.Empty;
        var service = CreateWingetCommandService(
            wingetRunner: static (singleArg, args, onOutputLine) => new WingetCommandResult
            {
                ExitCode = -1978335189,
                Output = "No available upgrade found."
            });
        var runner = new OperationRunner(service, new InstallCommandBuilder(service));

        await runner.RunUpdatesAsync(
            new[]
            {
                new UpdateEntry
                {
                    Name = "AxCrypt 2.1.1693.0",
                    Id = "AxCrypt.AxCrypt",
                    Version = "2.1.1693.0",
                    Available = "3.0.94",
                    Source = "winget",
                    IsSelected = true
                }
            },
            (_, state) => status = state.RawText,
            output.Add,
            (_, _) => { },
            LocalizedStrings.Italian,
            (_, errorMessage, resolutionHint) =>
            {
                error = errorMessage;
                resolution = resolutionHint;
            });

        Assert.Equal("Aggiornamento segnalato non applicato", status);
        Assert.Equal("Aggiornamento segnalato non applicato", error);
        Assert.Contains("2.1.1693.0 -> 3.0.94", resolution, StringComparison.Ordinal);
        Assert.Contains(output, line => line.Contains("event=winget_upgrade_noop", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunUpdatesAsync_ReportsNoApplicableUpgrade_WhenWingetSaysNewerVersionDoesNotApply()
    {
        var output = new List<string>();
        var status = string.Empty;
        var error = string.Empty;
        var resolution = string.Empty;
        var service = CreateWingetCommandService(
            wingetRunner: static (singleArg, args, onOutputLine) => new WingetCommandResult
            {
                ExitCode = -1978335189,
                Output = """
No applicable upgrade found.
A newer package version is available in a configured source, but it does not apply to your system or requirements.
"""
            });
        var runner = new OperationRunner(service, new InstallCommandBuilder(service));

        await runner.RunUpdatesAsync(
            new[]
            {
                new UpdateEntry
                {
                    Name = "WinRAR",
                    Id = "RARLab.WinRAR",
                    Source = "winget",
                    IsSelected = true
                }
            },
            (_, value) => status = RenderStatus(value, LocalizedStrings.English),
            output.Add,
            (_, _) => { },
            LocalizedStrings.English,
            (_, errorMessage, resolutionHint) =>
            {
                error = errorMessage;
                resolution = resolutionHint;
            });

        Assert.Equal("Upgrade not applicable", status);
        Assert.Equal("Upgrade not applicable", error);
        Assert.Contains("does not apply to this system", resolution, StringComparison.Ordinal);
        Assert.Contains(output, line => line.Contains("No applicable upgrade found.", StringComparison.Ordinal));
        Assert.Contains(output, line => line.Contains("newer package version", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(output, line => string.Equals(line.Trim(), "No update available", StringComparison.Ordinal));
        Assert.Contains(output, line => line.Contains("event=winget_upgrade_noop", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunUpdatesAsync_DoesNotDuplicateLiveNoApplicableUpgradeOutput()
    {
        var output = new List<string>();
        var service = CreateWingetCommandService(
            wingetRunner: static (singleArg, args, onOutputLine) =>
            {
                onOutputLine?.Invoke("No applicable upgrade found.");
                onOutputLine?.Invoke("A newer package version is available in a configured source, but it does not apply to your system or requirements.");
                return new WingetCommandResult
                {
                    ExitCode = -1978335189,
                    Output = """
No applicable upgrade found.
A newer package version is available in a configured source, but it does not apply to your system or requirements.
"""
                };
            });
        var runner = new OperationRunner(service, new InstallCommandBuilder(service));

        await runner.RunUpdatesAsync(
            new[]
            {
                new UpdateEntry
                {
                    Name = "WinRAR",
                    Id = "RARLab.WinRAR",
                    Source = "winget",
                    IsSelected = true
                }
            },
            (_, _) => { },
            output.Add,
            (_, _) => { },
            LocalizedStrings.English);

        Assert.Equal(1, output.Count(line => string.Equals(line.Trim(), "No applicable upgrade found.", StringComparison.Ordinal)));
        Assert.Equal(1, output.Count(line => line.Contains("newer package version", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(output, line => string.Equals(line.Trim(), "No update available", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunApplyAsync_ReportsInstallFailure_WhenInstallCommandFails()
    {
        var invokedCommands = new List<string>();
        var service = CreateWingetCommandService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                var command = singleArg ?? args[0];
                invokedCommands.Add(command);
                return command switch
                {
                    "show" => new WingetCommandResult { ExitCode = 0, Output = "Found VS Code [Microsoft.VisualStudioCode]" },
                    "install" => new WingetCommandResult { ExitCode = -1978335224, Output = "download failed" },
                    _ => new WingetCommandResult { ExitCode = 0, Output = string.Empty }
                };
            });
        var runner = new OperationRunner(service, new InstallCommandBuilder(service));
        var status = string.Empty;
        var strings = LocalizedStrings.English;

        await runner.RunApplyAsync(
            new[]
            {
                new AppEntry { Name = "VS Code", Id = "Microsoft.VisualStudioCode", Action = AppActions.Install }
            },
            (_, value) => status = RenderStatus(value, strings),
            _ => { },
            (_, _) => { },
            strings);

        Assert.Contains("show", invokedCommands);
        Assert.Single(invokedCommands, command => command == "install");
        Assert.Equal("Download failed", status);
    }

    [Fact]
    public async Task RunApplyAsync_UsesAlreadyInstalledStatus_WhenInstallDetectsExistingExternalApp()
    {
        var service = CreateWingetCommandService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                var command = singleArg ?? args[0];
                return command switch
                {
                    "install" => new WingetCommandResult { ExitCode = -1978334963, Output = "Another version of this application is already installed." },
                    _ => new WingetCommandResult { ExitCode = 0, Output = string.Empty }
                };
            });
        var runner = new OperationRunner(service, new InstallCommandBuilder(service));
        var status = string.Empty;
        var strings = LocalizedStrings.English;

        await runner.RunApplyAsync(
            new[]
            {
                new AppEntry { Name = "7-Zip", Id = "7zip.7zip", Action = AppActions.Install }
            },
            (_, value) => status = RenderStatus(value, strings),
            _ => { },
            (_, _) => { },
            strings);

        Assert.Equal("Already installed", status);
    }

    [Fact]
    public async Task RunApplyAsync_UsesLatestVersionStatus_WhenInstallReportsNoUpgradeNeeded()
    {
        var service = CreateWingetCommandService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                var command = singleArg ?? args[0];
                return command switch
                {
                    "install" => new WingetCommandResult { ExitCode = -1978335189, Output = "No available upgrade found." },
                    _ => new WingetCommandResult { ExitCode = 0, Output = string.Empty }
                };
            });
        var runner = new OperationRunner(service, new InstallCommandBuilder(service));
        var status = string.Empty;
        var error = "previous error";
        var resolution = "previous resolution";
        var strings = LocalizedStrings.English;

        await runner.RunApplyAsync(
            new[]
            {
                new AppEntry { Name = "VS Code", Id = "Microsoft.VisualStudioCode", Action = AppActions.Install }
            },
            (_, value) => status = RenderStatus(value, strings),
            _ => { },
            (_, _) => { },
            strings,
            (_, errorMessage, resolutionHint) =>
            {
                error = errorMessage;
                resolution = resolutionHint;
            });

        Assert.Equal("Latest version installed", status);
        Assert.Equal(string.Empty, error);
        Assert.Equal(string.Empty, resolution);
    }

    [Fact]
    public async Task RunApplyAsync_DoesNotRetryWithoutInstallerSelectors_WhenWingetReportsNoApplicableInstaller()
    {
        var installInvocations = new List<IReadOnlyList<string>>();
        var output = new List<string>();
        var errorMessage = string.Empty;
        var resolution = string.Empty;
        var service = CreateWingetCommandService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                var command = singleArg ?? args[0];
                if (command != "install")
                {
                    return new WingetCommandResult { ExitCode = 0, Output = string.Empty };
                }

                installInvocations.Add(args.ToArray());
                if (args.Contains("--architecture"))
                {
                    return new WingetCommandResult
                    {
                        ExitCode = -1978335216,
                        Output = "No applicable installer found; see logs for more details."
                    };
                }

                throw new InvalidOperationException("Install retry without installer selectors should not run.");
            });
        var runner = new OperationRunner(service, new InstallCommandBuilder(service), isCurrentProcessElevated: true);
        var status = string.Empty;
        var strings = LocalizedStrings.English;

        await runner.RunApplyAsync(
            new[]
            {
                new AppEntry
                {
                    Name = ".NET Framework Developer Pack",
                    Id = "Microsoft.DotNet.Framework.DeveloperPack.4.6",
                    Source = "winget",
                    Action = AppActions.Install,
                    Scope = "machine",
                    Architecture = "x64",
                    Locale = "en-US",
                    InstallerType = "burn",
                    InstallMode = InstallModes.SilentWithProgress
                }
            },
            (_, value) => status = RenderStatus(value, strings),
            output.Add,
            (_, _) => { },
            strings,
            (_, message, hint) =>
            {
                errorMessage = message;
                resolution = hint;
            });

        Assert.Equal("No applicable installer", status);
        Assert.Equal("No applicable installer", errorMessage);
        Assert.Contains("OnlyWinget did not retry without these constraints", resolution, StringComparison.Ordinal);
        Assert.Contains("scope=machine", resolution, StringComparison.Ordinal);
        Assert.Contains("architecture=x64", resolution, StringComparison.Ordinal);
        Assert.Contains("locale=en-US", resolution, StringComparison.Ordinal);
        Assert.Contains("installer-type=burn", resolution, StringComparison.Ordinal);
        Assert.Single(installInvocations);
        Assert.Contains("--scope", installInvocations[0]);
        Assert.Contains("--architecture", installInvocations[0]);
        Assert.Contains("--installer-type", installInvocations[0]);
        Assert.Contains("--locale", installInvocations[0]);
        Assert.DoesNotContain(output, line => line.Contains("event=install_retry_without_installer_selectors", StringComparison.Ordinal));
        Assert.Contains(output, line => line.Contains("event=install_no_applicable_installer_preserved_selectors", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunApplyAsync_DoesNotPinInstallToVersion()
    {
        var installInvocations = new List<IReadOnlyList<string>>();
        var service = CreateWingetCommandService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                var command = singleArg ?? args[0];
                if (command != "install")
                {
                    return new WingetCommandResult { ExitCode = 0, Output = string.Empty };
                }

                installInvocations.Add(args.ToArray());
                return new WingetCommandResult { ExitCode = 0, Output = "Successfully installed" };
            });
        var runner = new OperationRunner(service, new InstallCommandBuilder(service));
        var status = string.Empty;

        await runner.RunApplyAsync(
            new[]
            {
                new AppEntry
                {
                    Name = "AnyDesk",
                    Id = "AnyDesk.AnyDesk",
                    Source = "winget",
                    Action = AppActions.Install
                }
            },
            (_, value) => status = RenderStatus(value, LocalizedStrings.English),
            _ => { },
            (_, _) => { },
            LocalizedStrings.English);

        Assert.Equal("OK", status);
        var installArgs = Assert.Single(installInvocations);
        Assert.DoesNotContain("--version", installArgs);
        Assert.Contains("--id", installArgs);
        Assert.Contains("AnyDesk.AnyDesk", installArgs);
    }

    [Fact]
    public void UpgradeWinget_DoesNotConvertAlreadyInstalledIntoSuccess()
    {
        var invocations = 0;
        var service = CreateWingetCommandService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                var command = singleArg ?? args[0];
                invocations++;

                return command switch
                {
                    "upgrade" => new WingetCommandResult
                    {
                        ExitCode = -1978335212,
                        Output = "No installed package found matching input criteria."
                    },
                    "install" => new WingetCommandResult
                    {
                        ExitCode = -1978334963,
                        Output = "Another version of this application is already installed."
                    },
                    _ => new WingetCommandResult { ExitCode = 0, Output = string.Empty }
                };
            });

        var result = service.UpgradeWinget();

        Assert.True(invocations >= 2);
        Assert.Equal(-1978334963, result.ExitCode);
        Assert.Contains("already installed", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PackageOperationService_Install_UsesElevatedLauncher_WhenMachineScopeRequiresElevation()
    {
        var directInvocations = new List<IReadOnlyList<string>>();
        IReadOnlyList<string> elevatedArgs = Array.Empty<string>();
        var service = CreateWingetCommandService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                directInvocations.Add(args.ToArray());
                return new WingetCommandResult { ExitCode = 0, Output = string.Empty };
            });
        var elevatedLauncher = new FakeElevatedWingetLauncher((args, logPath, onOutputLine, cancellationToken) =>
        {
            elevatedArgs = args.ToArray();
            return new WingetCommandResult { ExitCode = 0, Output = "event=elevated_launch_completed exit_code=0" };
        });
        var operationService = new PackageOperationService(service, new InstallCommandBuilder(service), elevatedLauncher, isCurrentProcessElevated: false);

        var result = await operationService.ExecuteAsync(
            new PackageOperationRequest
            {
                Kind = PackageOperationKind.Install,
                OperationKey = "Contoso.Tool|winget|x64",
                Name = "Contoso Tool",
                Id = "Contoso.Tool",
                Source = "winget",
                Scope = "machine",
                Architecture = "x64"
            },
            LocalizedStrings.English);

        Assert.Equal(PackageOperationOutcome.Succeeded, result.Outcome);
        Assert.Contains(directInvocations, args => args.Count > 0 && args[0] == "show");
        Assert.Contains("install", elevatedArgs);
        Assert.Equal(PackageOperationExecutionMode.Elevated, result.ExecutionMode);
    }

    [Fact]
    public async Task PackageOperationService_Uninstall_IncludesConfiguredSource()
    {
        IReadOnlyList<string> uninstallArgs = Array.Empty<string>();
        var service = CreateWingetCommandService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                if ((singleArg ?? args[0]) == "uninstall")
                {
                    uninstallArgs = args.ToArray();
                }

                return new WingetCommandResult { ExitCode = 0, Output = "uninstalled" };
            });
        var operationService = new PackageOperationService(service, new InstallCommandBuilder(service), isCurrentProcessElevated: true);

        var result = await operationService.ExecuteAsync(
            new PackageOperationRequest
            {
                Kind = PackageOperationKind.Uninstall,
                OperationKey = "9WZDNCRFJBBG|msstore",
                Name = "Windows Camera",
                Id = "9WZDNCRFJBBG",
                Source = "msstore"
            },
            LocalizedStrings.English);

        Assert.Equal(PackageOperationOutcome.Succeeded, result.Outcome);
        Assert.Contains("--source", uninstallArgs);
        Assert.Contains("msstore", uninstallArgs);
    }

    [Fact]
    public async Task PackageOperationService_BlocksUnreviewedAdvancedArguments_BeforeWingetResolution()
    {
        var invoked = false;
        var service = CreateWingetCommandService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                invoked = true;
                return new WingetCommandResult { ExitCode = 0, Output = string.Empty };
            });
        var operationService = new PackageOperationService(service, new InstallCommandBuilder(service));

        var result = await operationService.ExecuteAsync(
            new PackageOperationRequest
            {
                Kind = PackageOperationKind.Install,
                OperationKey = "Contoso.Tool|winget",
                Name = "Contoso Tool",
                Id = "Contoso.Tool",
                Source = "winget",
                OverrideArgs = "/token secret",
                AdvancedArgumentsReviewed = false
            },
            LocalizedStrings.English);

        Assert.False(invoked);
        Assert.Equal(PackageOperationOutcome.AdvancedArgumentsReviewRequired, result.Outcome);
        Assert.Equal(LocalizedStrings.English.AdvancedArgumentsReviewRequiredText, result.Message);
    }

    public void Dispose()
    {
        foreach (var path in _temporaryPaths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch
            {
                // Non mascherare l'esito del test per errori di cleanup locale.
            }
        }
    }

    private WingetCommandService CreateWingetCommandService(
        Func<string?, IReadOnlyList<string>, Action<string>?, WingetCommandResult> wingetRunner,
        Func<DateTime>? utcNow = null)
    {
        return new WingetCommandService(
            wingetRunner: wingetRunner,
            localRuntimeRoot: CreateTempDirectory(),
            utcNow: utcNow);
    }

    private string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "OnlyWinget.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _temporaryPaths.Add(path);
        return path;
    }

    private static string RenderStatus(UiStatusState state, LocalizedStrings strings)
    {
        var entry = new AppEntry();
        entry.ApplyStatus(state, strings);
        return entry.Status;
    }

    private static IReadOnlyList<string> ParseWindowsCommandLine(string commandLine)
    {
        var argv = CommandLineToArgvW(commandLine, out var argc);
        if (argv == IntPtr.Zero)
        {
            throw new InvalidOperationException("CommandLineToArgvW failed.");
        }

        try
        {
            var args = new string[argc];
            for (var index = 0; index < argc; index++)
            {
                var pointer = Marshal.ReadIntPtr(argv, index * IntPtr.Size);
                args[index] = Marshal.PtrToStringUni(pointer) ?? string.Empty;
            }

            return args;
        }
        finally
        {
            LocalFree(argv);
        }
    }

    private sealed class FakeElevatedWingetLauncher : IElevatedWingetLauncher
    {
        private readonly Func<IReadOnlyList<string>, string?, Action<string>?, System.Threading.CancellationToken, WingetCommandResult> _launch;

        public FakeElevatedWingetLauncher(Func<IReadOnlyList<string>, string?, Action<string>?, System.Threading.CancellationToken, WingetCommandResult> launch)
        {
            _launch = launch;
        }

        public WingetCommandResult Launch(
            IReadOnlyList<string> args,
            string? logFilePath,
            Action<string>? onOutputLine = null,
            TimeSpan? timeout = null,
            System.Threading.CancellationToken cancellationToken = default)
        {
            return _launch(args, logFilePath, onOutputLine, cancellationToken);
        }
    }

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern IntPtr CommandLineToArgvW(
        [MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine,
        out int pNumArgs);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);
}
