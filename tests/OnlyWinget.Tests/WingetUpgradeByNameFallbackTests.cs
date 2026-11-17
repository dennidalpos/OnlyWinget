using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Operations;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Infrastructure.Winget;

namespace OnlyWinget.Tests;

public sealed class WingetUpgradeByNameFallbackTests
{
    private const int NotFound = unchecked((int)0x8A150014);

    [Fact]
    public async Task UpgradeNotMatchedByIdIsRetriedByInstalledName()
    {
        var runner = new ScriptedRunner(new WingetCommandResult(NotFound, "no package", string.Empty), new WingetCommandResult(0, "ok", string.Empty));
        var executor = new WingetOperationExecutor(runner, new WingetCommandBuilder(), new WingetErrorClassifier(), TimeSpan.Zero);
        var selection = new PackageSelection(new PackageIdentity("Microsoft.PowerShell", "winget"), PackageAction.Upgrade, "PowerShell 7.6.10.0-x64");

        var summary = await executor.ExecuteAsync(new OperationPlan("test", [selection]), CancellationToken.None);

        Assert.True(summary.Succeeded);
        Assert.Equal(2, runner.Calls.Count);
        Assert.Contains("--id", runner.Calls[0]);
        Assert.DoesNotContain("--id", runner.Calls[1]);
        Assert.Equal("PowerShell 7.6.10.0-x64", runner.Calls[1][runner.Calls[1].ToList().IndexOf("--name") + 1]);
    }

    [Fact]
    public async Task UpgradeWithoutInstalledNameIsNotRetried()
    {
        var runner = new ScriptedRunner(new WingetCommandResult(NotFound, "no package", string.Empty));
        var executor = new WingetOperationExecutor(runner, new WingetCommandBuilder(), new WingetErrorClassifier(), TimeSpan.Zero);
        var selection = new PackageSelection(new PackageIdentity("Some.Package", "winget"), PackageAction.Upgrade);

        var summary = await executor.ExecuteAsync(new OperationPlan("test", [selection]), CancellationToken.None);

        Assert.False(summary.Succeeded);
        Assert.Single(runner.Calls);
    }

    [Fact]
    public void TechnologyMismatchHasActionableGuidance() =>
        Assert.Contains("Uninstall", WingetErrorGuidance.For(WingetErrorKind.InstallTechnologyMismatch, PackageAction.Upgrade));

    private sealed class ScriptedRunner(params WingetCommandResult[] results) : IWingetCommandRunner
    {
        public List<IReadOnlyList<string>> Calls { get; } = [];
        public Task<WingetCommandResult> RunAsync(string command, IReadOnlyList<string> arguments,
            CancellationToken cancellationToken, IProgress<WingetProgress>? progress = null, TimeSpan? timeout = null)
        {
            Calls.Add(arguments.ToArray());
            return Task.FromResult(results[Math.Min(Calls.Count - 1, results.Length - 1)]);
        }
    }
}
