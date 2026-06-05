// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OnlyWinget.Models;
using OnlyWinget.Services;
using Xunit;

namespace OnlyWinget.Tests;

public sealed class WingetPackageInterrogationServiceTests
{
    [Fact]
    public async Task InterrogateAsync_ParsesBaseMetadata_AndFallsBackWhenManifestMissing()
    {
        var service = CreateService(
            showOutput: """
Trovato Microsoft PowerToys [Microsoft.PowerToys]
Versione: 0.90.1
Programma di installazione:
  Tipo di programma di installazione: exe
""",
            manifestStatusCode: HttpStatusCode.NotFound);

        var result = await service.InterrogateAsync(new PackageInterrogationRequest
        {
            PackageId = "Microsoft.PowerToys",
            Source = "winget"
        });

        Assert.True(result.Success);
        Assert.True(result.IsReducedMode);
        Assert.Equal("Microsoft PowerToys", result.Name);
        Assert.Equal("Microsoft.PowerToys", result.Id);
        Assert.Equal("0.90.1", result.Version);
        Assert.Equal("exe", result.InstallerType);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public async Task InterrogateAsync_ParsesLocalizedPackageHeaderFallback()
    {
        var service = CreateService(
            showOutput: """
Gefunden Contoso Tool [Contoso.Tool]
Version: 2.0.0
Installer Type: exe
""",
            manifestStatusCode: HttpStatusCode.NotFound);

        var result = await service.InterrogateAsync(new PackageInterrogationRequest
        {
            PackageId = "Contoso.Tool",
            Source = "winget"
        });

        Assert.True(result.Success);
        Assert.Equal("Contoso Tool", result.Name);
        Assert.Equal("Contoso.Tool", result.Id);
        Assert.True(result.IsReducedMode);
    }

    [Fact]
    public async Task InterrogateAsync_Fails_WhenPackageIsAmbiguous()
    {
        var service = CreateService(
            showOutput: "Multiple packages found matching input criteria.",
            manifestStatusCode: HttpStatusCode.NotFound);

        var result = await service.InterrogateAsync(new PackageInterrogationRequest
        {
            PackageId = "Git",
            Source = "winget"
        });

        Assert.False(result.Success);
        Assert.Contains("resolved uniquely", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InterrogateAsync_AppliesNodePrecedenceOverRootDefaults()
    {
        var service = CreateService(
            showOutput: """
Found Git [Git.Git]
Version: 2.53.0.2
Installer Type: inno
""",
            manifestContent: """
PackageIdentifier: Git.Git
PackageVersion: 2.53.0.2
InstallerType: msi
Scope: user
InstallerLocale: en-US
InstallerSwitches:
  Silent: /quiet
Installers:
- Architecture: x64
  Scope: machine
  InstallerLocale: it-IT
  InstallerType: exe
  InstallerSwitches:
    SilentWithProgress: /passive
ManifestType: installer
""");

        var result = await service.InterrogateAsync(new PackageInterrogationRequest
        {
            PackageId = "Git.Git",
            Source = "winget"
        });

        Assert.True(result.Success);
        Assert.False(result.IsReducedMode);
        Assert.Single(result.InstallerOptions);
        Assert.Equal("machine", result.InstallerOptions[0].Scope);
        Assert.Equal("it-IT", result.InstallerOptions[0].Locale);
        Assert.Equal("exe", result.InstallerOptions[0].InstallerType);
        Assert.True(result.InstallerOptions[0].SupportsSilent);
        Assert.True(result.InstallerOptions[0].SupportsSilentWithProgress);
        Assert.False(string.IsNullOrWhiteSpace(result.ManifestFingerprint));
    }

    [Fact]
    public async Task InterrogateAsync_ParsesIndentedWingetInstallerFixtures()
    {
        var service = CreateService(
            showOutput: """
Found Contoso Tool [Contoso.Tool]
Version: 2.0.0
Installer Type: exe
""",
            manifestContent: """
PackageIdentifier: Contoso.Tool
PackageVersion: 2.0.0
InstallerType: "exe" # root default
InstallModes: [silent, silentWithProgress]
Installers:
  - Architecture: x64
    Scope: machine
    InstallerLocale: "en-US"
    InstallerSwitches:
      Silent: "/quiet"
      SilentWithProgress: "/passive"
  - Architecture: x86
    Scope: user
    InstallerLocale: 'it-IT'
    UnsupportedArguments: [log]
ManifestType: installer
""",
            architectureProvider: () => "x64",
            cultureProvider: () => CultureInfo.GetCultureInfo("en-US"));

        var result = await service.InterrogateAsync(new PackageInterrogationRequest
        {
            PackageId = "Contoso.Tool",
            Source = "winget"
        });

        Assert.True(result.Success);
        Assert.False(result.IsReducedMode);
        Assert.Equal(2, result.InstallerOptions.Count);
        Assert.Equal("x64", result.DefaultSelection.Architecture);
        Assert.Equal("machine", result.DefaultSelection.Scope);
        Assert.Equal("en-US", result.DefaultSelection.Locale);
        Assert.True(result.InstallerOptions[0].SupportsSilent);
        Assert.True(result.InstallerOptions[0].SupportsSilentWithProgress);
        Assert.Equal("exe", result.InstallerOptions[0].InstallerType);
    }

    [Fact]
    public async Task InterrogateAsync_ParsesQuotedInlineYamlSequences_WithCommas()
    {
        var service = CreateService(
            showOutput: """
Found Contoso Tool [Contoso.Tool]
Version: 2.0.0
Installer Type: exe
""",
            manifestContent: """
PackageIdentifier: Contoso.Tool
PackageVersion: 2.0.0
InstallerType: exe
Installers:
  - Architecture: x64
    UnsupportedArguments: ["log, path", override] # comment outside sequence
ManifestType: installer
""");

        var result = await service.InterrogateAsync(new PackageInterrogationRequest
        {
            PackageId = "Contoso.Tool",
            Source = "winget"
        });

        Assert.True(result.Success);
        var option = Assert.Single(result.InstallerOptions);
        Assert.Contains("log, path", option.UnsupportedArguments);
        Assert.Contains("override", option.UnsupportedArguments);
    }

    [Fact]
    public async Task InterrogateAsync_UsesReducedMode_WhenManifestUsesUnsupportedYamlScalar()
    {
        var logs = new List<string>();
        var service = CreateService(
            showOutput: """
Found Contoso Tool [Contoso.Tool]
Version: 2.0.0
Installer Type: exe
""",
            manifestContent: """
PackageIdentifier: Contoso.Tool
PackageVersion: 2.0.0
InstallerType: exe
Installers:
  - Architecture: x64
    InstallerSwitches:
      Silent: >
        /quiet
ManifestType: installer
""");

        var result = await service.InterrogateAsync(new PackageInterrogationRequest
        {
            PackageId = "Contoso.Tool",
            Source = "winget",
            Log = logs.Add
        });

        Assert.True(result.Success);
        Assert.True(result.IsReducedMode);
        Assert.Empty(result.InstallerOptions);
        Assert.Contains(result.Warnings, warning => warning.Contains("unsupported YAML", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(logs, line => line.Contains("event=manifest_parse_failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InterrogateAsync_UsesReducedMode_WhenManifestUsesYamlMergeKeys()
    {
        var service = CreateService(
            showOutput: """
Found Contoso Tool [Contoso.Tool]
Version: 2.0.0
Installer Type: exe
""",
            manifestContent: """
PackageIdentifier: Contoso.Tool
PackageVersion: 2.0.0
InstallerType: exe
Installers:
  - Architecture: x64
    <<: *installerDefaults
ManifestType: installer
""");

        var result = await service.InterrogateAsync(new PackageInterrogationRequest
        {
            PackageId = "Contoso.Tool",
            Source = "winget"
        });

        Assert.True(result.Success);
        Assert.True(result.IsReducedMode);
        Assert.Empty(result.InstallerOptions);
        Assert.Contains(result.Warnings, warning => warning.Contains("unsupported YAML", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task InterrogateAsync_SkipsManifestFetch_WhenManifestPathMetadataIsUnsafe()
    {
        var requestCount = 0;
        var wingetService = new WingetService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                var command = singleArg ?? args[0];
                return command switch
                {
                    "show" => new WingetCommandResult
                    {
                        ExitCode = 0,
                        Output = """
Found Contoso Tool [Contoso/Tool]
Version: 1.0.0
Installer Type: exe
"""
                    },
                    "list" => new WingetCommandResult { ExitCode = 0, Output = string.Empty },
                    _ => new WingetCommandResult { ExitCode = 0, Output = string.Empty }
                };
            });
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("PackageIdentifier: Contoso/Tool")
            };
        }));
        var logs = new List<string>();
        var service = new WingetPackageInterrogationService(wingetService, httpClient);

        var result = await service.InterrogateAsync(new PackageInterrogationRequest
        {
            PackageId = "Contoso/Tool",
            Source = "winget",
            Log = logs.Add
        });

        Assert.True(result.Success);
        Assert.True(result.IsReducedMode);
        Assert.Equal(0, requestCount);
        Assert.Contains(result.Warnings, warning => warning.Contains("skipped", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(logs, line => line.Contains("event=manifest_url_rejected", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InterrogateAsync_SanitizesStructuredLogValues()
    {
        var logs = new List<string>();
        var service = CreateService(
            showOutput: "Multiple packages found matching input criteria.",
            manifestStatusCode: HttpStatusCode.NotFound);

        var result = await service.InterrogateAsync(new PackageInterrogationRequest
        {
            PackageId = "Contoso.Tool\r\nevent=fake",
            Source = "winget",
            Log = logs.Add
        });

        Assert.False(result.Success);
        Assert.NotEmpty(logs);
        Assert.DoesNotContain(logs, line => line.Contains('\r') || line.Contains('\n'));
        Assert.Contains(logs, line => line.Contains("Contoso.Tool  event=fake", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InterrogateAsync_DoesNotTreatNextInstallerAsUnsupportedArgumentListItem()
    {
        var service = CreateService(
            showOutput: """
Found Contoso Tool [Contoso.Tool]
Version: 2.0.0
Installer Type: exe
""",
            manifestContent: """
PackageIdentifier: Contoso.Tool
PackageVersion: 2.0.0
InstallerType: exe
Installers:
  - Architecture: x64
    UnsupportedArguments:
      - log
  - Architecture: x86
ManifestType: installer
""");

        var result = await service.InterrogateAsync(new PackageInterrogationRequest
        {
            PackageId = "Contoso.Tool",
            Source = "winget"
        });

        Assert.True(result.Success);
        Assert.Equal(2, result.InstallerOptions.Count);
        Assert.Contains(result.InstallerOptions, option => option.Architecture == "x64");
        Assert.Contains(result.InstallerOptions, option => option.Architecture == "x86");
    }

    [Fact]
    public async Task InterrogateAsync_DoesNotWarn_WhenInstallerNodesExposeOneSelectableChoice()
    {
        var service = CreateService(
            showOutput: """
Found AnyDesk [AnyDesk.AnyDesk]
Version: 9.7.1
Installer Type: exe
""",
            manifestContent: """
PackageIdentifier: AnyDesk.AnyDesk
PackageVersion: 9.7.1
InstallerType: exe
Installers:
- Architecture: x86
  InstallerUrl: https://example.invalid/anydesk-a.exe
- Architecture: x86
  InstallerUrl: https://example.invalid/anydesk-b.exe
ManifestType: installer
""");

        var result = await service.InterrogateAsync(new PackageInterrogationRequest
        {
            PackageId = "AnyDesk.AnyDesk",
            Source = "winget"
        });

        Assert.True(result.Success);
        Assert.DoesNotContain(result.Warnings, warning => warning.Contains("Multiple installer", StringComparison.OrdinalIgnoreCase));
        Assert.Single(result.AvailableArchitectures);
        Assert.Single(result.AvailableInstallerTypes);
    }

    [Fact]
    public async Task InterrogateAsync_Warns_WhenInstallerNodesExposeDifferentSelectableChoices()
    {
        var service = CreateService(
            showOutput: """
Found Sample App [Contoso.Sample]
Version: 1.0.0
Installer Type: exe
""",
            manifestContent: """
PackageIdentifier: Contoso.Sample
PackageVersion: 1.0.0
InstallerType: exe
Installers:
- Architecture: x86
- Architecture: x64
ManifestType: installer
""");

        var result = await service.InterrogateAsync(new PackageInterrogationRequest
        {
            PackageId = "Contoso.Sample",
            Source = "winget"
        });

        Assert.True(result.Success);
        Assert.Contains(result.Warnings, warning => warning.Contains("Multiple installer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task InterrogateAsync_DoesNotPassSavedVersion()
    {
        var invocations = new List<IReadOnlyList<string>>();
        var wingetService = new WingetService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                invocations.Add(args.ToArray());
                return new WingetCommandResult
                {
                    ExitCode = 0,
                    Output = """
Found AnyDesk [AnyDesk.AnyDesk]
Version: 9.7.1
Installer Type: exe
"""
                };
            });
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        var service = new WingetPackageInterrogationService(wingetService, httpClient);

        var result = await service.InterrogateAsync(new PackageInterrogationRequest
        {
            PackageId = "AnyDesk.AnyDesk",
            Source = "winget"
        });

        Assert.True(result.Success);
        Assert.Equal("9.7.1", result.Version);
        var showInvocations = invocations.Where(args => args.Count > 0 && args[0] == "show").ToList();
        var showInvocation = Assert.Single(showInvocations);
        Assert.DoesNotContain("--version", showInvocation);
    }

    [Fact]
    public async Task InterrogateAsync_PrefersCurrentArchitectureThenLocaleForDefaultSelection()
    {
        var service = CreateService(
            showOutput: """
Found Sample App [Contoso.Sample]
Version: 1.0.0
Installer Type: exe
""",
            manifestContent: """
PackageIdentifier: Contoso.Sample
PackageVersion: 1.0.0
InstallerType: exe
Installers:
- Architecture: x86
  InstallerLocale: en-US
- Architecture: x64
  InstallerLocale: it-IT
- Architecture: x64
  InstallerLocale: en-US
ManifestType: installer
""",
            architectureProvider: () => "x64",
            cultureProvider: () => CultureInfo.GetCultureInfo("en-US"));

        var result = await service.InterrogateAsync(new PackageInterrogationRequest
        {
            PackageId = "Contoso.Sample",
            Source = "winget"
        });

        Assert.True(result.Success);
        Assert.Equal("x64", result.DefaultSelection.Architecture);
        Assert.Equal("en-US", result.DefaultSelection.Locale);
    }

    [Fact]
    public async Task InterrogateAsync_InstalledDetailsOutrankManifestOrder()
    {
        var service = CreateService(
            showOutput: """
Found Sample App [Contoso.Sample]
Version: 1.0.0
Installer Type: exe
""",
            manifestContent: """
PackageIdentifier: Contoso.Sample
PackageVersion: 1.0.0
InstallerType: msi
Installers:
- Architecture: x86
  Scope: user
  InstallerLocale: en-US
  InstallerType: msi
- Architecture: x64
  Scope: machine
  InstallerLocale: it-IT
  InstallerType: exe
ManifestType: installer
""",
            architectureProvider: () => "x86",
            cultureProvider: () => CultureInfo.GetCultureInfo("en-US"),
            listOutput: """
Name: Sample App
Id: Contoso.Sample
Installed Scope: Machine
Installed Architecture: x64
Installer Locale: it-IT
Installer Type: exe
""");

        var result = await service.InterrogateAsync(new PackageInterrogationRequest
        {
            PackageId = "Contoso.Sample",
            Source = "winget"
        });

        Assert.True(result.Success);
        Assert.Equal("machine", result.DefaultSelection.Scope);
        Assert.Equal("x64", result.DefaultSelection.Architecture);
        Assert.Equal("it-IT", result.DefaultSelection.Locale);
        Assert.Equal("exe", result.DefaultSelection.InstallerType);
    }

    [Fact]
    public async Task InterrogateAsync_UsesCultureFallbackWhenInstalledLocaleMissing()
    {
        var service = CreateService(
            showOutput: """
Found Sample App [Contoso.Sample]
Version: 1.0.0
Installer Type: exe
""",
            manifestContent: """
PackageIdentifier: Contoso.Sample
PackageVersion: 1.0.0
InstallerType: exe
Installers:
- Architecture: x64
  InstallerLocale: en-US
- Architecture: x64
  InstallerLocale: fr-FR
ManifestType: installer
""",
            architectureProvider: () => "x64",
            cultureProvider: () => CultureInfo.GetCultureInfo("fr-FR"));

        var result = await service.InterrogateAsync(new PackageInterrogationRequest
        {
            PackageId = "Contoso.Sample",
            Source = "winget"
        });

        Assert.True(result.Success);
        Assert.Equal("fr-FR", result.DefaultSelection.Locale);
    }

    [Fact]
    public async Task InterrogateAsync_ContinuesWhenInstalledDetailsLookupFails()
    {
        var wingetService = new WingetService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                var command = singleArg ?? args[0];
                return command switch
                {
                    "show" => new WingetCommandResult
                    {
                        ExitCode = 0,
                        Output = """
Found Sample App [Contoso.Sample]
Version: 1.0.0
Installer Type: exe
"""
                    },
                    "list" => throw new InvalidOperationException("list failed"),
                    _ => new WingetCommandResult { ExitCode = 0, Output = string.Empty }
                };
            });
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
PackageIdentifier: Contoso.Sample
PackageVersion: 1.0.0
InstallerType: exe
Installers:
- Architecture: x64
ManifestType: installer
""")
        }));
        var service = new WingetPackageInterrogationService(
            wingetService,
            httpClient,
            architectureProvider: () => "x64",
            cultureProvider: () => CultureInfo.GetCultureInfo("en-US"));

        var result = await service.InterrogateAsync(new PackageInterrogationRequest
        {
            PackageId = "Contoso.Sample",
            Source = "winget"
        });

        Assert.True(result.Success);
        Assert.Equal("x64", result.DefaultSelection.Architecture);
    }

    [Fact]
    public async Task InterrogateAsync_RetriesTransientManifestFetchFailure()
    {
        var requestCount = 0;
        var wingetService = CreateSuccessfulShowWingetService();
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
PackageIdentifier: Contoso.Tool
PackageVersion: 1.0.0
InstallerType: exe
Installers:
- Architecture: x64
ManifestType: installer
""")
            };
        }));
        var service = new WingetPackageInterrogationService(
            wingetService,
            httpClient,
            manifestRetryBaseDelay: TimeSpan.Zero);

        var result = await service.InterrogateAsync(new PackageInterrogationRequest
        {
            PackageId = "Contoso.Tool",
            Source = "winget"
        });

        Assert.True(result.Success);
        Assert.False(result.IsReducedMode);
        Assert.Equal(2, requestCount);
    }

    [Fact]
    public async Task InterrogateAsync_CachesSuccessfulManifestFetches()
    {
        var requestCount = 0;
        var wingetService = CreateSuccessfulShowWingetService();
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
PackageIdentifier: Contoso.Tool
PackageVersion: 1.0.0
InstallerType: exe
Installers:
- Architecture: x64
ManifestType: installer
""")
            };
        }));
        var service = new WingetPackageInterrogationService(wingetService, httpClient);
        var request = new PackageInterrogationRequest
        {
            PackageId = "Contoso.Tool",
            Source = "winget"
        };

        var firstResult = await service.InterrogateAsync(request);
        var secondResult = await service.InterrogateAsync(request);

        Assert.True(firstResult.Success);
        Assert.True(secondResult.Success);
        Assert.False(firstResult.IsReducedMode);
        Assert.False(secondResult.IsReducedMode);
        Assert.Equal(1, requestCount);
    }

    [Fact]
    public async Task InterrogateAsync_FallsBackWhenManifestResponseExceedsMaxSize()
    {
        var oversizedManifest = """
PackageIdentifier: Contoso.Tool
PackageVersion: 1.0.0
InstallerType: exe
Installers:
- Architecture: x64
ManifestType: installer
""";
        var wingetService = CreateSuccessfulShowWingetService();
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Encoding.UTF8.GetBytes(oversizedManifest))
        }));
        var service = new WingetPackageInterrogationService(
            wingetService,
            httpClient,
            manifestMaxBytes: 32);

        var result = await service.InterrogateAsync(new PackageInterrogationRequest
        {
            PackageId = "Contoso.Tool",
            Source = "winget"
        });

        Assert.True(result.Success);
        Assert.True(result.IsReducedMode);
        Assert.Contains(result.Warnings, warning => warning.Contains("could not be retrieved", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task InterrogateAsync_FallsBackWhenManifestFetchTimesOut()
    {
        var manifestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var manifestCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var httpClient = new HttpClient(new BlockingHttpMessageHandler(manifestStarted, manifestCancelled));
        var service = new WingetPackageInterrogationService(
            CreateSuccessfulShowWingetService(),
            httpClient,
            manifestFetchTimeout: TimeSpan.FromMilliseconds(50),
            manifestMaxAttempts: 1,
            manifestRetryBaseDelay: TimeSpan.Zero);

        var result = await service.InterrogateAsync(new PackageInterrogationRequest
        {
            PackageId = "Contoso.Tool",
            Source = "winget"
        });

        Assert.True(result.Success);
        Assert.True(result.IsReducedMode);
        await manifestStarted.Task;
        await manifestCancelled.Task;
    }

    [Fact]
    public async Task InterrogateAsync_PropagatesCancellationToWingetShow()
    {
        var showStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var showCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var wingetService = new WingetService(
            (singleArg, args, onOutputLine, cancellationToken) =>
            {
                var command = singleArg ?? args[0];
                if (command == "show")
                {
                    showStarted.TrySetResult();
                    try
                    {
                        Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).GetAwaiter().GetResult();
                    }
                    catch (OperationCanceledException)
                    {
                        showCancelled.TrySetResult();
                        throw;
                    }
                }

                return new WingetCommandResult { ExitCode = 0, Output = string.Empty };
            });
        var service = new WingetPackageInterrogationService(
            wingetService,
            new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))));
        using var cancellation = new CancellationTokenSource();

        var interrogation = service.InterrogateAsync(new PackageInterrogationRequest
        {
            PackageId = "Contoso.Tool",
            Source = "winget"
        }, cancellation.Token);
        await showStarted.Task;

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => interrogation);
        await showCancelled.Task;
    }

    [Fact]
    public async Task InterrogateAsync_PropagatesCancellationToManifestFetch()
    {
        var manifestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var manifestCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var wingetService = new WingetService(
            (singleArg, args, onOutputLine, cancellationToken) =>
            {
                var command = singleArg ?? args[0];
                return command switch
                {
                    "show" => new WingetCommandResult
                    {
                        ExitCode = 0,
                        Output = """
Found Contoso Tool [Contoso.Tool]
Version: 1.0.0
Installer Type: exe
"""
                    },
                    "list" => new WingetCommandResult { ExitCode = 0, Output = string.Empty },
                    _ => new WingetCommandResult { ExitCode = 0, Output = string.Empty }
                };
            });
        var httpClient = new HttpClient(new BlockingHttpMessageHandler(manifestStarted, manifestCancelled));
        var service = new WingetPackageInterrogationService(wingetService, httpClient);
        using var cancellation = new CancellationTokenSource();

        var interrogation = service.InterrogateAsync(new PackageInterrogationRequest
        {
            PackageId = "Contoso.Tool",
            Source = "winget"
        }, cancellation.Token);
        await manifestStarted.Task;

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => interrogation);
        await manifestCancelled.Task;
    }

    private static WingetPackageInterrogationService CreateService(
        string showOutput,
        HttpStatusCode manifestStatusCode,
        string manifestContent = "",
        Func<string>? architectureProvider = null,
        Func<CultureInfo>? cultureProvider = null)
    {
        return CreateService(showOutput, manifestContent, architectureProvider, cultureProvider, manifestStatusCode);
    }

    private static WingetPackageInterrogationService CreateService(
        string showOutput,
        string manifestContent,
        Func<string>? architectureProvider = null,
        Func<CultureInfo>? cultureProvider = null,
        HttpStatusCode manifestStatusCode = HttpStatusCode.OK,
        string listOutput = "")
    {
        var wingetService = new WingetService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                var command = singleArg ?? args[0];
                return command switch
                {
                    "show" => new WingetCommandResult { ExitCode = 0, Output = showOutput },
                    "list" => new WingetCommandResult { ExitCode = 0, Output = listOutput },
                    _ => new WingetCommandResult { ExitCode = 0, Output = string.Empty }
                };
            });

        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(manifestStatusCode)
        {
            Content = new StringContent(manifestContent ?? string.Empty)
        }));

        return new WingetPackageInterrogationService(
            wingetService,
            httpClient,
            architectureProvider,
            cultureProvider);
    }

    private static WingetService CreateSuccessfulShowWingetService()
    {
        return new WingetService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                var command = singleArg ?? args[0];
                return command switch
                {
                    "show" => new WingetCommandResult
                    {
                        ExitCode = 0,
                        Output = """
Found Contoso Tool [Contoso.Tool]
Version: 1.0.0
Installer Type: exe
"""
                    },
                    "list" => new WingetCommandResult { ExitCode = 0, Output = string.Empty },
                    _ => new WingetCommandResult { ExitCode = 0, Output = string.Empty }
                };
            });
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    private sealed class BlockingHttpMessageHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _started;
        private readonly TaskCompletionSource _cancelled;

        public BlockingHttpMessageHandler(TaskCompletionSource started, TaskCompletionSource cancelled)
        {
            _started = started;
            _cancelled = cancelled;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _cancelled.TrySetResult();
                throw;
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
