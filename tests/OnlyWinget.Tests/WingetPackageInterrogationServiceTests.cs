// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
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
    public async Task InterrogateAsync_RetriesWithoutVersion_WhenPinnedVersionIsNoLongerAvailable()
    {
        var invocations = new List<IReadOnlyList<string>>();
        var wingetService = new WingetService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                invocations.Add(args.ToArray());
                return args.Contains("--version")
                    ? new WingetCommandResult { ExitCode = -1978335212, Output = "No version found matching: 9.7.0" }
                    : new WingetCommandResult
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
            Source = "winget",
            Version = "9.7.0"
        });

        Assert.True(result.Success);
        Assert.Equal("9.7.1", result.Version);
        Assert.Equal(2, invocations.Count);
        Assert.Contains("--version", invocations[0]);
        Assert.DoesNotContain("--version", invocations[1]);
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
        HttpStatusCode manifestStatusCode = HttpStatusCode.OK)
    {
        var wingetService = new WingetService(
            wingetRunner: (singleArg, args, onOutputLine) =>
            {
                var command = singleArg ?? args[0];
                return command switch
                {
                    "show" => new WingetCommandResult { ExitCode = 0, Output = showOutput },
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
}
