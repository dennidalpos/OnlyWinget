using OnlyWinget.Application.System;
using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Infrastructure.System;
using OnlyWinget.Infrastructure.Winget;

namespace OnlyWinget.Tests;

public sealed class CapabilityCancellationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task CapabilityProbePropagatesCancellationAndStopsFurtherCommands(int blockedCall)
    {
        var runner = new BlockingRunner(blockedCall);
        var service = new SystemCapabilityService(runner);
        using var cancellation = new CancellationTokenSource();
        var pending = service.GetCapabilitiesAsync(cancellation.Token);
        await runner.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.Equal(blockedCall + 1, runner.Calls);
        }
        finally
        {
            runner.Release.TrySetResult();
        }
    }

    [Fact]
    public async Task InstalledStatusPropagatesCancellation()
    {
        var runner = new BlockingRunner(0);
        var resolver = new WingetPackageResolver(runner, new WingetTableParser(), new WingetErrorClassifier());
        using var cancellation = new CancellationTokenSource();
        var pending = resolver.CheckInstalledStatusAsync(new PackageIdentity("Example.App"), cancellation.Token);
        await runner.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.Equal(1, runner.Calls);
        }
        finally
        {
            runner.Release.TrySetResult();
        }
    }

    [Fact]
    public async Task PreCancelledChecksDoNotStartProcesses()
    {
        var runner = new BlockingRunner(-1);
        var capabilities = new SystemCapabilityService(runner);
        var resolver = new WingetPackageResolver(runner, new WingetTableParser(), new WingetErrorClassifier());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capabilities.GetCapabilitiesAsync(cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            resolver.CheckInstalledStatusAsync(new PackageIdentity("Example.App"), cancellation.Token));
        Assert.Equal(0, runner.Calls);
    }

    private sealed class BlockingRunner(int blockedCall) : IExternalProcessRunner, IWingetCommandRunner
    {
        public int Calls { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ExternalProcessResult> RunAsync(string command, IReadOnlyList<string> arguments,
            CancellationToken cancellationToken, IProgress<string>? standardOutputLines = null,
            TimeSpan? timeout = null, IProgress<string>? standardErrorLines = null)
        {
            if (Calls++ == blockedCall)
            {
                Started.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return new ExternalProcessResult(0, "available", string.Empty);
        }

        async Task<WingetCommandResult> IWingetCommandRunner.RunAsync(string command, IReadOnlyList<string> arguments,
            CancellationToken cancellationToken, IProgress<WingetProgress>? progress, TimeSpan? timeout)
        {
            var result = await RunAsync(command, arguments, cancellationToken, timeout: timeout);
            return new WingetCommandResult(result.ExitCode, result.StandardOutput, result.StandardError);
        }
    }
}
