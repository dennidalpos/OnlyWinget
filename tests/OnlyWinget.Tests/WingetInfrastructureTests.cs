using OnlyWinget.Application.Operations;
using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Presets;
using OnlyWinget.Infrastructure.Winget;

namespace OnlyWinget.Tests;

public sealed class WingetInfrastructureTests
{
    [Fact]
    public async Task CommandAvailabilityRunsWingetVersion()
    {
        var runner = new RecordingWingetCommandRunner(
            new WingetCommandResult(0, "v1.9.0", string.Empty));
        var availability = new CommandAvailability(runner);

        var isAvailable = await availability.IsWingetAvailableAsync(CancellationToken.None);

        Assert.True(isAvailable);
        Assert.Equal("winget", runner.LastCommand);
        Assert.Equal(["--version"], runner.LastArguments);
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
        var service = new WingetPackageSearchService(runner, new WingetTableParser());

        var results = await service.SearchAsync(new PackageSearchRequest("git", "winget"), CancellationToken.None);

        Assert.Equal(["search", "git", "--accept-source-agreements", "--source", "winget"], runner.LastArguments);
        Assert.Equal(2, results.Count);
        Assert.Equal("Git.Git", results[0].Package.Id);
        Assert.Equal("winget", results[0].Package.Source);
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
        var loader = new WingetUpdateLoader(runner, new WingetTableParser());

        var updates = await loader.LoadUpdatesAsync(CancellationToken.None);

        Assert.Equal(["upgrade", "--accept-source-agreements"], runner.LastArguments);
        var update = Assert.Single(updates);
        Assert.Equal("Git.Git", update.Package.Id);
        Assert.Equal("2.0.0", update.InstalledVersion);
        Assert.Equal("2.1.0", update.AvailableVersion);
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
            return Task.FromResult(results.Count == 0
                ? new WingetCommandResult(0, string.Empty, string.Empty)
                : results.Dequeue());
        }
    }
}
