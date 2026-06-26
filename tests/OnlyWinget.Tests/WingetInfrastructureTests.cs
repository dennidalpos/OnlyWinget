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
        var runner = new RecordingWingetCommandRunner(
            new WingetCommandResult(0, "v1.9.0", string.Empty),
            new WingetCommandResult(0, "5.1.0", string.Empty),
            new WingetCommandResult(0, "available", string.Empty));
        var availability = new SystemCapabilityService(runner);

        var capabilities = await availability.GetCapabilitiesAsync(CancellationToken.None);

        Assert.True(capabilities.IsWingetAvailable);
        Assert.True(capabilities.IsPowerShellAvailable);
        Assert.True(capabilities.IsWindowsUpdateComAvailable);
        Assert.Contains(runner.CommandCalls, call => call.Command == "winget" && call.Arguments.SequenceEqual(["--version"]));
        Assert.Contains(runner.CommandCalls, call => call.Command == "powershell.exe");
    }

    [Fact]
    public async Task WindowsUpdateServiceReturnsFailureWithoutRunningPowerShellWhenCapabilityIsMissing()
    {
        var runner = new RecordingWingetCommandRunner();
        var service = new PowerShellWindowsUpdateService(
            runner,
            new StubSystemCapabilityService(new SystemCapabilities(true, true, false, false, null)));

        var outcome = await service.ScanAsync(CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Contains("PowerShell is not available", outcome.Error?.Message, StringComparison.Ordinal);
        Assert.Empty(runner.Calls);
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
        var noUpdates = classifier.Classify(new WingetCommandResult(1, string.Empty, "No applicable update found."));
        var source = classifier.Classify(new WingetCommandResult(1, string.Empty, "Failed when searching source: winget"));

        Assert.Equal(WingetErrorKind.NotFound, notFound?.Kind);
        Assert.Equal(WingetErrorKind.NoUpdates, noUpdates?.Kind);
        Assert.Equal(WingetErrorKind.SourceUnavailable, source?.Kind);
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

        Assert.Equal(["search", "git", "--accept-source-agreements", "--source", "winget"], runner.LastArguments);
        Assert.True(outcome.Succeeded);
        Assert.Equal(2, outcome.Rows.Count);
        Assert.Equal("Git.Git", outcome.Rows[0].Package.Id);
        Assert.Equal("winget", outcome.Rows[0].Package.Source);
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
            """;
        var runner = new RecordingWingetCommandRunner(new WingetCommandResult(0, output, string.Empty));
        var resolver = new WingetPackageResolver(runner, new WingetErrorClassifier());

        var resolution = await resolver.ResolveAsync(new PackageIdentity("Git.Git"), CancellationToken.None);

        Assert.Equal(["show", "--id", "Git.Git", "--exact", "--accept-source-agreements"], runner.LastArguments);
        Assert.True(resolution.IsResolved);
        Assert.Equal("2.0.0", resolution.Version);
        Assert.Equal("winget", resolution.Package.Source);
        Assert.Null(resolution.Error);
    }

    [Fact]
    public async Task UpdateLoaderRunsWingetUpgradeAndMapsRows()
    {
        const string output = """
            Name  Id       Version  Available  Source
            ------------------------------------------
            Git   Git.Git  2.0.0    2.1.0      winget
            """;
        var runner = new RecordingWingetCommandRunner(new WingetCommandResult(0, output, string.Empty));
        var loader = new WingetUpdateLoader(runner, new WingetTableParser(), new WingetErrorClassifier());

        var outcome = await loader.LoadUpdatesAsync(CancellationToken.None);

        Assert.Equal(["upgrade", "--accept-source-agreements"], runner.LastArguments);
        var update = Assert.Single(outcome.Rows);
        Assert.Equal("Git.Git", update.Package.Id);
        Assert.Equal("2.0.0", update.InstalledVersion);
        Assert.Equal("2.1.0", update.AvailableVersion);
    }

    [Fact]
    public async Task SourceServiceRunsSourceCommandsAndMapsLocalizedRows()
    {
        const string output = """
            Nome   Argomento                              Contenuti espliciti
            -----------------------------------------------------------------
            winget https://cdn.winget.microsoft.com/cache false
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

        var source = Assert.Single(sources.Rows);
        Assert.Equal("winget", source.Name);
        Assert.Equal("https://cdn.winget.microsoft.com/cache", source.Argument);
        Assert.Equal(["source", "reset", "--force"], runner.LastArguments);
        Assert.Contains(runner.Calls, call => call.SequenceEqual(["source", "update", "--accept-source-agreements"]));
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

        var summary = await executor.ExecuteAsync(plan, CancellationToken.None);

        Assert.False(summary.Succeeded);
        Assert.Equal(2, summary.Results.Count);
        Assert.Equal(WingetErrorKind.NotFound, summary.Results[1].Error?.Kind);
        Assert.Equal("Missing.App", summary.Results[1].Selection.Package.Id);
        Assert.Equal(2, runner.Calls.Count);
    }

    private sealed class RecordingWingetCommandRunner(params WingetCommandResult[] results) : IWingetCommandRunner
    {
        private readonly Queue<WingetCommandResult> results = new(results);

        public List<IReadOnlyList<string>> Calls { get; } = [];

        public List<CommandCall> CommandCalls { get; } = [];

        public string? LastCommand { get; private set; }

        public IReadOnlyList<string>? LastArguments { get; private set; }

        public Task<WingetCommandResult> RunAsync(
            string command,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            LastCommand = command;
            LastArguments = arguments.ToArray();
            Calls.Add(LastArguments);
            CommandCalls.Add(new CommandCall(command, LastArguments));
            return Task.FromResult(results.Count == 0
                ? new WingetCommandResult(0, string.Empty, string.Empty)
                : results.Dequeue());
        }
    }

    private sealed record CommandCall(string Command, IReadOnlyList<string> Arguments);

    private sealed class StubSystemCapabilityService(SystemCapabilities capabilities) : ISystemCapabilityService
    {
        public Task<SystemCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(capabilities);
    }
}
