using OnlyWinget.Application.Operations;
using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Presets;
using OnlyWinget.Infrastructure.Winget;

namespace OnlyWinget.Tests;

public sealed class WingetBatchCancellationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task CancellationKeepsCompletedResultsAndMarksActiveAndUnstartedPackages(int firstExitCode)
    {
        var runner = new BlockingWingetCommandRunner(new WingetCommandResult(firstExitCode, "first output", "first diagnostic"));
        var executor = new WingetOperationExecutor(runner, new(), new());
        var plan = new OperationPlanner().CreatePresetPlan(new Preset("Batch",
            [new("One.App", "winget"), new("Two.App", "winget"), new("Three.App", "winget")]), PackageAction.Upgrade);
        using var cancellation = new CancellationTokenSource();
        var execution = executor.ExecuteAsync(plan, cancellation.Token, continueAfterFailure: true);
        await runner.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        var exception = await Assert.ThrowsAsync<OperationExecutionCanceledException>(() => execution);
        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(3, exception.Summary.Results.Count);
        var completed = exception.Summary.Results[0];
        Assert.Equal(firstExitCode == 0, completed.Succeeded);
        Assert.Equal("first output", completed.CommandResult.StandardOutput);
        Assert.Equal("first diagnostic", completed.CommandResult.StandardError);
        Assert.Equal(WingetErrorKind.Cancelled, exception.Summary.Results[1].Error?.Kind);
        Assert.Equal(1, exception.Summary.Results[1].AttemptCount);
        Assert.Equal(WingetErrorKind.Cancelled, exception.Summary.Results[2].Error?.Kind);
        Assert.Equal(0, exception.Summary.Results[2].AttemptCount);
        Assert.Equal(2, runner.Calls.Count);
    }

    [Fact]
    public async Task CancellationBetweenCommandsKeepsCompletedSuccess()
    {
        using var cancellation = new CancellationTokenSource();
        var runner = new CancelAfterResultRunner(cancellation);
        var executor = new WingetOperationExecutor(runner, new(), new());
        var plan = new OperationPlanner().CreatePresetPlan(new Preset("Batch", [new("One.App"), new("Two.App")]), PackageAction.Install);
        var exception = await Assert.ThrowsAsync<OperationExecutionCanceledException>(() => executor.ExecuteAsync(plan, cancellation.Token));
        Assert.True(exception.Summary.Results[0].Succeeded);
        Assert.Equal(0, exception.Summary.Results[1].AttemptCount);
        Assert.Equal(1, runner.CallCount);
    }

    [Fact]
    public async Task CancellationDuringRetryWaitKeepsLastFailureDiagnosticsWithoutRetrying()
    {
        using var cancellation = new CancellationTokenSource();
        var runner = new CancelAfterResultRunner(cancellation, fail: true);
        var executor = new WingetOperationExecutor(runner, new(), new(), retryDelay: TimeSpan.FromSeconds(30));
        var plan = new OperationPlanner().CreatePresetPlan(new Preset("Batch", [new("One.App")]), PackageAction.Install);
        var exception = await Assert.ThrowsAsync<OperationExecutionCanceledException>(() => executor.ExecuteAsync(plan, cancellation.Token, maxRetries: 2));
        var result = Assert.Single(exception.Summary.Results);
        Assert.False(result.Succeeded);
        Assert.Equal(WingetErrorKind.Cancelled, result.Error?.Kind);
        Assert.Equal("Transient error opening source", result.CommandResult.StandardError);
        Assert.Equal(1, result.AttemptCount);
        Assert.Equal(1, runner.CallCount);
    }

    private sealed class CancelAfterResultRunner(CancellationTokenSource cancellation, bool fail = false) : IWingetCommandRunner
    {
        public int CallCount { get; private set; }
        public Task<WingetCommandResult> RunAsync(string command, IReadOnlyList<string> arguments,
            CancellationToken cancellationToken, IProgress<WingetProgress>? progress = null, TimeSpan? timeout = null)
        {
            CallCount++;
            cancellation.Cancel();
            return Task.FromResult(fail ? new WingetCommandResult(1, string.Empty, "Transient error opening source")
                : new WingetCommandResult(0, "installed", string.Empty));
        }
    }
}

internal sealed class BlockingWingetCommandRunner(WingetCommandResult? firstResult = null) : IWingetCommandRunner
{
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public List<IReadOnlyList<string>> Calls { get; } = [];
    public async Task<WingetCommandResult> RunAsync(string command, IReadOnlyList<string> arguments,
        CancellationToken cancellationToken, IProgress<WingetProgress>? progress = null, TimeSpan? timeout = null)
    {
        Calls.Add(arguments.ToArray());
        if (Calls.Count == 1) return firstResult ?? new WingetCommandResult(0, "upgraded", string.Empty);
        Started.TrySetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("The blocking test command must be cancelled.");
    }
}
