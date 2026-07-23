using OnlyWinget.Application.Operations;
using OnlyWinget.Application.System;
using OnlyWinget.Application.Winget;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Infrastructure.System;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Presets;
using OnlyWinget.Infrastructure.WindowsUpdate;
using OnlyWinget.Infrastructure.Winget;

namespace OnlyWinget.Tests;

public sealed class WingetInfrastructureTests
{
    [Fact]
    public async Task SystemCapabilityServiceChecksRequiredCommandsAndWindowsUpdateCom()
    {
        var runner = new RecordingExternalProcessRunner(
            new ExternalProcessResult(0, "v1.9.0", string.Empty),
            new ExternalProcessResult(0, "5.1.0", string.Empty),
            new ExternalProcessResult(0, "available", string.Empty));
        var availability = new SystemCapabilityService(runner);

        var capabilities = await availability.GetCapabilitiesAsync(CancellationToken.None);

        Assert.True(capabilities.IsWingetAvailable);
        Assert.True(capabilities.IsPowerShellAvailable);
        Assert.True(capabilities.IsWindowsUpdateComAvailable);
        Assert.Equal("v1.9.0", capabilities.WingetVersion);
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(Environment.OSVersion.Version.Build, capabilities.WindowsBuildNumber);
        }
        Assert.Contains(runner.CommandCalls, call => call.Command == "winget" && call.Arguments.SequenceEqual(["--version"]));
        Assert.Contains(runner.CommandCalls, call => call.Command == "powershell.exe");
    }

    [Fact]
    public async Task WindowsUpdateServiceReturnsFailureWithoutRunningPowerShellWhenCapabilityIsMissing()
    {
        var runner = new RecordingExternalProcessRunner();
        var service = new PowerShellWindowsUpdateService(
            runner,
            new StubSystemCapabilityService(new SystemCapabilities(true, true, false, false, null)));

        var outcome = await service.ScanAsync(new WindowsUpdateOptions(), CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Contains("PowerShell is not available", outcome.Error?.Message, StringComparison.Ordinal);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task WindowsUpdateOptionsConfigureDriversAndMicrosoftUpdateWithoutSupersededContent()
    {
        var runner = new RecordingExternalProcessRunner(
            new ExternalProcessResult(0, "{\"succeeded\":true,\"rows\":[],\"error\":null}", string.Empty));
        var service = new PowerShellWindowsUpdateService(
            runner,
            new StubSystemCapabilityService(new SystemCapabilities(true, true, true, true, null)));

        var outcome = await service.ScanAsync(
            new WindowsUpdateOptions(false, true, true),
            CancellationToken.None);

        Assert.True(outcome.Succeeded);
        var encoded = Assert.IsAssignableFrom<IReadOnlyList<string>>(runner.LastArguments).Last();
        var script = System.Text.Encoding.Unicode.GetString(Convert.FromBase64String(encoded));
        Assert.Contains("IsInstalled=0 and IsHidden=0", script, StringComparison.Ordinal);
        Assert.Contains("Type=\\u0027Driver\\u0027", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Type=\\u0027Software\\u0027", script, StringComparison.Ordinal);
        Assert.Contains("7971f918-a847-4430-9279-4a52d1efe18d", script, StringComparison.Ordinal);
        Assert.Contains("$serviceManager.Services", script, StringComparison.Ordinal);
        Assert.DoesNotContain("AddService2", script, StringComparison.Ordinal);
        Assert.DoesNotContain("includePotentiallySupersededUpdates", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WindowsUpdateServiceMapsPatchMetadata()
    {
        const string output = """
            {"succeeded":true,"rows":[{"updateId":"update-1","revisionNumber":3,"title":"Security update","description":"Fixes","severity":"Critical","categories":["Security"],"knowledgeBaseArticles":["5000001"],"maxDownloadSize":12345678,"isDownloaded":false,"rebootRequired":true}],"error":null}
            """;
        var runner = new RecordingExternalProcessRunner(
            new ExternalProcessResult(0, output, string.Empty));
        var service = new PowerShellWindowsUpdateService(
            runner,
            new StubSystemCapabilityService(new SystemCapabilities(true, true, true, true, null)));

        var outcome = await service.ScanAsync(new WindowsUpdateOptions(), CancellationToken.None);

        var update = Assert.Single(outcome.Rows);
        Assert.Equal(["5000001"], update.KnowledgeBaseArticles);
        Assert.Equal(12_345_678UL, update.MaxDownloadSize);
        Assert.True(update.RebootRequired);
    }

    [Fact]
    public void OperationPlannerCreatesPresetPlan()
    {
        var planner = new OperationPlanner();
        var preset = new Preset("Default", [new PackageIdentity("Git.Git", "winget")]);

        var plan = planner.CreatePresetPlan(preset, PackageAction.Install);

        Assert.Equal("Default", plan.Name);
        Assert.True(plan.HasWork);
        Assert.Equal(PackageAction.Install, plan.Selections.Single().Action);
        Assert.Equal("Git.Git", plan.Selections.Single().Package.Id);
    }

    [Fact]
    public void ErrorClassifierDetectsKnownWingetFailures()
    {
        var classifier = new WingetErrorClassifier();

        var notFound = classifier.Classify(new WingetCommandResult(1, string.Empty, "No package found matching input criteria."));
        var notFoundIt = classifier.Classify(new WingetCommandResult(1, "Nessun pacchetto trovato con criteri di input corrispondenti.", string.Empty));
        var noUpdates = classifier.Classify(new WingetCommandResult(1, string.Empty, "No applicable update found."));
        var noUpdatesIt = classifier.Classify(new WingetCommandResult(1, string.Empty, "Non è stato trovato alcun aggiornamento applicabile. Una versione più recente del pacchetto è disponibile in un'origine configurata, ma non si applica al sistema o ai requisiti."));
        var noUpdatesEngMsg = classifier.Classify(new WingetCommandResult(1, string.Empty, "A newer version of the package is available in a configured source, but does not apply to the system or requirements."));
        var noInstalledIt = classifier.Classify(new WingetCommandResult(1, "Non è stato trovato alcun pacchetto installato corrispondente ai criteri di input.", string.Empty));
        var explicitTargetIt = classifier.Classify(new WingetCommandResult(1, "Per i pacchetti seguenti è disponibile un aggiornamento, ma è necessario un targeting esplicito per l'aggiornamento:", string.Empty));
        var source = classifier.Classify(new WingetCommandResult(1, string.Empty, "Failed when searching source: winget"));

        Assert.Equal(WingetErrorKind.NotFound, notFound?.Kind);
        Assert.Equal(WingetErrorKind.NotFound, notFoundIt?.Kind);
        Assert.Equal(WingetErrorKind.NoUpdates, noUpdates?.Kind);
        Assert.Equal(WingetErrorKind.NoUpdates, noUpdatesIt?.Kind);
        Assert.Equal(WingetErrorKind.NoUpdates, noUpdatesEngMsg?.Kind);
        Assert.Equal(WingetErrorKind.NoUpdates, noInstalledIt?.Kind);
        Assert.Equal(WingetErrorKind.NoUpdates, explicitTargetIt?.Kind);
        Assert.Equal(WingetErrorKind.SourceUnavailable, source?.Kind);
    }

    [Theory]
    [InlineData("\u001b[32mDownloading 42%\u001b[0m", WingetProgressPhase.Downloading, 42, "Downloading 42%")]
    [InlineData("Scaricamento 7%", WingetProgressPhase.Downloading, 7, "Scaricamento 7%")]
    [InlineData("Installation 100%", WingetProgressPhase.Installing, 100, "Installation 100%")]
    [InlineData("Installing 101%", WingetProgressPhase.Installing, null, "Installing 101%")]
    [InlineData("Installing nope%", WingetProgressPhase.Installing, null, "Installing nope%")]
    [InlineData("\rDownloading 42%\r", WingetProgressPhase.Downloading, 42, "Downloading 42%")]
    [InlineData("Téléchargement 12%", WingetProgressPhase.Downloading, 12, "Téléchargement 12%")]
    [InlineData("Herunterladen 9%", WingetProgressPhase.Downloading, 9, "Herunterladen 9%")]
    public void ProgressParserHandlesAnsiLocalizedAndMalformedLines(
        string line,
        WingetProgressPhase expectedPhase,
        int? expectedPercentage,
        string expectedMessage)
    {
        var parsed = Assert.IsType<WingetProgress>(new WingetProgressParser().Parse(line));

        Assert.Equal(expectedPhase, parsed.Phase);
        Assert.Equal(expectedPercentage, parsed.Percentage);
        Assert.Equal(expectedMessage, parsed.Message);
    }

    [Fact]
    public void ProgressParserIgnoresEmptyAndAnsiOnlyLines()
    {
        var parser = new WingetProgressParser();

        Assert.Null(parser.Parse("\r\n"));
        Assert.Null(parser.Parse("\u001b[0m"));
    }

    [Fact]
    public async Task PackageSearchRunsWingetSearchAndMapsRows()
    {
        const string output = """
            Name       Id                 Version Match        Source
            ---------------------------------------------------------
            Git        Git.Git            2.0.0   Moniker: git winget
            PowerToys  Microsoft.PowerToys 1.0.0               winget
            """;
        var runner = new RecordingWingetCommandRunner(new WingetCommandResult(0, output, string.Empty));
        var service = new WingetPackageSearchService(runner, new WingetTableParser(), new WingetErrorClassifier());

        var outcome = await service.SearchAsync(new PackageSearchRequest("git", "winget"), CancellationToken.None);

        Assert.Equal(
            ["search", "git", "--count", "1000", "--accept-source-agreements", "--disable-interactivity", "--source", "winget"],
            runner.LastArguments);
        Assert.True(outcome.Succeeded);
        Assert.Equal(2, outcome.Rows.Count);
        Assert.Equal("Git.Git", outcome.Rows[0].Package.Id);
        Assert.Equal("winget", outcome.Rows[0].Package.Source);
    }

    [Fact]
    public async Task PackageSearchUsesRequestedSourceWhenWingetOmitsSourceColumn()
    {
        const string output = """
            Name Id      Version
            --------------------
            Git  Git.Git 2.54.0
            """;
        var runner = new RecordingWingetCommandRunner(new WingetCommandResult(0, output, string.Empty));
        var service = new WingetPackageSearchService(runner, new WingetTableParser(), new WingetErrorClassifier());

        var outcome = await service.SearchAsync(new PackageSearchRequest("git", "winget"), CancellationToken.None);

        Assert.Equal("winget", Assert.Single(outcome.Rows).Package.Source);
    }

    [Fact]
    public async Task PackageSearchMapsLocalizedRows()
    {
        const string output = """
            Nome Id      Versione Origine
            ------------------------------
            Git  Git.Git 2.54.0   winget
            """;
        var runner = new RecordingWingetCommandRunner(new WingetCommandResult(0, output, string.Empty));
        var service = new WingetPackageSearchService(runner, new WingetTableParser(), new WingetErrorClassifier());

        var outcome = await service.SearchAsync(new PackageSearchRequest("Git.Git"), CancellationToken.None);

        var result = Assert.Single(outcome.Rows);
        Assert.Equal("Git", result.Name);
        Assert.Equal("2.54.0", result.Version);
        Assert.Equal("winget", result.Package.Source);
    }

    [Fact]
    public async Task PackageSearchReturnsStructuredFailure()
    {
        var runner = new RecordingWingetCommandRunner(new WingetCommandResult(1, string.Empty, "Failed when searching source: winget"));
        var service = new WingetPackageSearchService(runner, new WingetTableParser(), new WingetErrorClassifier());

        var outcome = await service.SearchAsync(new PackageSearchRequest("git"), CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Equal(WingetErrorKind.SourceUnavailable, outcome.Error?.Kind);
    }

    [Fact]
    public async Task PackageResolverRunsWingetShowAndMapsDetails()
    {
        const string output = """
            Found Git [Git.Git]
            Version: 2.0.0
            Publisher: The Git Development Community
            Source: winget
            Architecture: x64
            Architecture: arm64
            """;
        var runner = new RecordingWingetCommandRunner(new WingetCommandResult(0, output, string.Empty));
        var resolver = new WingetPackageResolver(runner, new WingetTableParser(), new WingetErrorClassifier());

        var resolution = await resolver.ResolveAsync(new PackageIdentity("Git.Git"), CancellationToken.None);

        Assert.Equal(["show", "--id", "Git.Git", "--exact", "--accept-source-agreements", "--disable-interactivity"], runner.LastArguments);
        Assert.True(resolution.IsResolved);
        Assert.Equal("Git", resolution.Name);
        Assert.Equal("2.0.0", resolution.Version);
        Assert.Equal("The Git Development Community", resolution.Publisher);
        Assert.Equal("winget", resolution.Package.Source);
        Assert.Equal(["x64", "arm64"], resolution.Architectures);
        Assert.Null(resolution.Error);
    }

    [Theory]
    [InlineData("Autore", "Google LLC")]
    [InlineData("Editore", "VideoLAN")]
    public async Task PackageResolverMapsLocalizedPublisherFromShowOutput(string publisherLabel, string publisher)
    {
        var output = $"""
            Trovato VLC media player [VideoLAN.VLC]
            Versione: 3.0.23
            {publisherLabel}: {publisher}
            """;
        var runner = new RecordingWingetCommandRunner(new WingetCommandResult(0, output, string.Empty));
        var resolver = new WingetPackageResolver(runner, new WingetTableParser(), new WingetErrorClassifier());

        var resolution = await resolver.ResolveAsync(new PackageIdentity("VideoLAN.VLC"), CancellationToken.None);

        Assert.True(resolution.IsResolved);
        Assert.Equal("VLC media player", resolution.Name);
        Assert.Equal("3.0.23", resolution.Version);
        Assert.Equal(publisher, resolution.Publisher);
    }

    [Fact]
    public async Task UpdateLoaderRunsWingetUpgradeAndMapsRows()
    {
        const string output = """
            Name  Id       Version  Available
            ----------------------------------
            Git   Git.Git  2.0.0    2.1.0
            """;
        var runner = new RecordingWingetCommandRunner(new WingetCommandResult(0, output, string.Empty));
        var loader = new WingetUpdateLoader(runner, new WingetTableParser(), new WingetErrorClassifier());

        var outcome = await loader.LoadUpdatesAsync("winget", CancellationToken.None);

        Assert.Equal(["upgrade", "--source", "winget", "--accept-source-agreements", "--disable-interactivity"], runner.LastArguments);
        var update = Assert.Single(outcome.Rows);
        Assert.Equal("Git.Git", update.Package.Id);
        Assert.Equal("winget", update.Package.Source);
        Assert.Equal("2.0.0", update.InstalledVersion);
        Assert.Equal("2.1.0", update.AvailableVersion);
    }

    [Fact]
    public async Task UpdateLoaderIgnoresLocalizedMessagesAfterTheTable()
    {
        const string output = """
            Nome      Id                        Versione  Disponibile     Origine
            ---------------------------------------------------------------------
            CCleaner  Piriform.CCleaner.Slim    6.41      6.41.0.11567   winget
            Per i pacchetti seguenti è disponibile un aggiornamento, ma è necessario un targeting esplicito.
            """;
        var runner = new RecordingWingetCommandRunner(new WingetCommandResult(0, output, string.Empty));
        var loader = new WingetUpdateLoader(runner, new WingetTableParser(), new WingetErrorClassifier());

        var outcome = await loader.LoadUpdatesAsync("winget", CancellationToken.None);

        var update = Assert.Single(outcome.Rows);
        Assert.Equal("Piriform.CCleaner.Slim", update.Package.Id);
    }

    [Fact]
    public async Task SourceServiceRunsSourceCommandsAndMapsLocalizedRows()
    {
        const string output = """
            Nome        Argomento                                     Contenuti espliciti
            -----------------------------------------------------------------------------
            msstore     https://storeedgefd.dsx.mp.microsoft.com/v9.0 false
            winget      https://cdn.winget.microsoft.com/cache        false
            winget-font https://cdn.winget.microsoft.com/fonts        true
            """;
        var runner = new RecordingWingetCommandRunner(
            new WingetCommandResult(0, output, string.Empty),
            new WingetCommandResult(0, "Done", string.Empty),
            new WingetCommandResult(0, "Done", string.Empty),
            new WingetCommandResult(0, "Done", string.Empty),
            new WingetCommandResult(0, "Done", string.Empty));
        var service = new WingetSourceService(runner, new WingetTableParser(), new WingetErrorClassifier());

        var sources = await service.ListSourcesAsync(CancellationToken.None);
        await service.UpdateSourcesAsync(CancellationToken.None);
        await service.AddSourceAsync("custom", "https://example.test", CancellationToken.None);
        await service.RemoveSourceAsync("custom", CancellationToken.None);
        await service.ResetSourcesAsync(CancellationToken.None);

        var source = sources.Rows.Single(source => source.Name == "winget");
        Assert.Equal("winget", source.Name);
        Assert.Equal("https://cdn.winget.microsoft.com/cache", source.Argument);
        Assert.Equal(["source", "reset", "--force"], runner.LastArguments);
        Assert.Contains(runner.Calls, call => call.SequenceEqual(["source", "update"]));
        Assert.Contains(runner.Calls, call => call.SequenceEqual(["source", "add", "--name", "custom", "--arg", "https://example.test", "--accept-source-agreements"]));
        Assert.Contains(runner.Calls, call => call.SequenceEqual(["source", "remove", "--name", "custom"]));
    }

    [Fact]
    public async Task OperationExecutorRunsSelectionsInOrderAndKeepsFailures()
    {
        var runner = new RecordingWingetCommandRunner(
            new WingetCommandResult(0, "installed", string.Empty),
            new WingetCommandResult(1, string.Empty, "No package found matching input criteria."));
        var executor = new WingetOperationExecutor(
            runner,
            new WingetCommandBuilder(),
            new WingetErrorClassifier());
        var plan = new OperationPlanner().CreatePresetPlan(
            new Preset("Default", [new PackageIdentity("Git.Git"), new PackageIdentity("Missing.App")]),
            PackageAction.Install);

        var summary = await executor.ExecuteAsync(plan, CancellationToken.None, continueAfterFailure: true);

        Assert.False(summary.Succeeded);
        Assert.Equal(2, summary.Results.Count);
        Assert.Equal(WingetErrorKind.NotFound, summary.Results[1].Error?.Kind);
        Assert.Equal("Missing.App", summary.Results[1].Selection.Package.Id);
        Assert.Equal(2, runner.Calls.Count);
    }

    [Fact]
    public async Task OperationExecutorStopsAfterFailureByDefault()
    {
        var runner = new RecordingWingetCommandRunner(
            new WingetCommandResult(1, string.Empty, "failure"),
            new WingetCommandResult(0, "installed", string.Empty));
        var plan = new OperationPlanner().CreatePresetPlan(
            new Preset("Default", [new PackageIdentity("Broken.App"), new PackageIdentity("Next.App")]),
            PackageAction.Install);

        var summary = await new WingetOperationExecutor(
                runner,
                new WingetCommandBuilder(),
                new WingetErrorClassifier())
            .ExecuteAsync(plan, CancellationToken.None);

        Assert.Single(summary.Results);
        Assert.Single(runner.Calls);
        Assert.Equal("Broken.App", summary.Results[0].Selection.Package.Id);
    }

    [Fact]
    public async Task OperationExecutorAggregatesAndThrottlesMultiPackageProgress()
    {
        var runner = new RecordingWingetCommandRunner(
            new WingetCommandResult(0, string.Empty, string.Empty),
            new WingetCommandResult(0, string.Empty, string.Empty));
        runner.ProgressUpdates.AddRange(
        [
            new WingetProgress(WingetProgressPhase.Downloading, 50, null),
            new WingetProgress(WingetProgressPhase.Downloading, 50, null),
            new WingetProgress(WingetProgressPhase.Installing, 100, null)
        ]);
        var progress = new RecordingProgress<OperationProgress>();
        var plan = new OperationPlanner().CreatePresetPlan(
            new Preset("Two", [new PackageIdentity("One.App"), new PackageIdentity("Two.App")]),
            PackageAction.Install);

        await new WingetOperationExecutor(runner, new WingetCommandBuilder(), new WingetErrorClassifier())
            .ExecuteAsync(plan, CancellationToken.None, progress);

        Assert.Equal([25, 50, 75, 100], progress.Values.Select(value => value.Percentage));
    }

    [Fact]
    public async Task OperationExecutorHonorsCancellationBeforeRunningCommands()
    {
        var runner = new RecordingWingetCommandRunner();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var plan = new OperationPlanner().CreatePresetPlan(
            new Preset("Cancelled", [new PackageIdentity("One.App")]),
            PackageAction.Install);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new WingetOperationExecutor(runner, new WingetCommandBuilder(), new WingetErrorClassifier())
                .ExecuteAsync(plan, cancellation.Token));
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task OperationExecutorRetriesTransientFailuresUpToMaxRetries()
    {
        var runner = new RecordingWingetCommandRunner(
            new WingetCommandResult(1, string.Empty, "Transient error opening source"),
            new WingetCommandResult(0, "Successfully installed", string.Empty));
        var executor = new WingetOperationExecutor(
            runner,
            new WingetCommandBuilder(),
            new WingetErrorClassifier());
        var plan = new OperationPlanner().CreatePresetPlan(
            new Preset("Default", [new PackageIdentity("Retry.App")]),
            PackageAction.Install);

        var summary = await executor.ExecuteAsync(plan, CancellationToken.None, maxRetries: 2);

        Assert.True(summary.Succeeded);
        Assert.Single(summary.Results);
        Assert.Equal(2, summary.Results[0].AttemptCount);
        Assert.Equal(2, runner.Calls.Count);
    }

    [Fact]
    public async Task OperationExecutorDoesNotRetryNonRetryableErrors()
    {
        var runner = new RecordingWingetCommandRunner(
            new WingetCommandResult(1, string.Empty, "No package found matching input criteria."),
            new WingetCommandResult(0, "installed", string.Empty));
        var executor = new WingetOperationExecutor(
            runner,
            new WingetCommandBuilder(),
            new WingetErrorClassifier());
        var plan = new OperationPlanner().CreatePresetPlan(
            new Preset("Default", [new PackageIdentity("Missing.App")]),
            PackageAction.Install);

        var summary = await executor.ExecuteAsync(plan, CancellationToken.None, maxRetries: 2);

        Assert.False(summary.Succeeded);
        Assert.Single(summary.Results);
        Assert.Equal(1, summary.Results[0].AttemptCount);
        Assert.Single(runner.Calls);
    }

    private sealed class RecordingWingetCommandRunner(params WingetCommandResult[] results) : IWingetCommandRunner
    {
        private readonly Queue<WingetCommandResult> results = new(results);

        public List<IReadOnlyList<string>> Calls { get; } = [];

        public List<CommandCall> CommandCalls { get; } = [];

        public string? LastCommand { get; private set; }

        public IReadOnlyList<string>? LastArguments { get; private set; }

        public List<WingetProgress> ProgressUpdates { get; } = [];

        public Task<WingetCommandResult> RunAsync(
            string command,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken,
            IProgress<WingetProgress>? progress = null,
            TimeSpan? timeout = null)
        {
            LastCommand = command;
            LastArguments = arguments.ToArray();
            Calls.Add(LastArguments);
            CommandCalls.Add(new CommandCall(command, LastArguments));
            foreach (var update in ProgressUpdates)
            {
                progress?.Report(update);
            }

            return Task.FromResult(results.Count == 0
                ? new WingetCommandResult(0, string.Empty, string.Empty)
                : results.Dequeue());
        }
    }

    private sealed class RecordingExternalProcessRunner(params ExternalProcessResult[] results) : IExternalProcessRunner
    {
        private readonly Queue<ExternalProcessResult> results = new(results);

        public List<IReadOnlyList<string>> Calls { get; } = [];

        public List<CommandCall> CommandCalls { get; } = [];

        public IReadOnlyList<string>? LastArguments { get; private set; }

        public Task<ExternalProcessResult> RunAsync(
            string command,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken,
            IProgress<string>? standardOutputLines = null,
            TimeSpan? timeout = null)
        {
            LastArguments = arguments.ToArray();
            Calls.Add(LastArguments);
            CommandCalls.Add(new CommandCall(command, LastArguments));
            return Task.FromResult(results.Count == 0
                ? new ExternalProcessResult(0, string.Empty, string.Empty)
                : results.Dequeue());
        }
    }

    private sealed record CommandCall(string Command, IReadOnlyList<string> Arguments);

    private sealed class RecordingProgress<T> : IProgress<T>
    {
        public List<T> Values { get; } = [];

        public void Report(T value) => Values.Add(value);
    }

    private sealed class StubSystemCapabilityService(SystemCapabilities capabilities) : ISystemCapabilityService
    {
        public Task<SystemCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(capabilities);
    }
}
