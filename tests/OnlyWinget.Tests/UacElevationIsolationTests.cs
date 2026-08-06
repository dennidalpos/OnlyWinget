using OnlyWinget.Application.System;
using OnlyWinget.Application.Winget;
using OnlyWinget.Infrastructure.System;
using OnlyWinget.Infrastructure.Winget;

namespace OnlyWinget.Tests;

public sealed class UacElevationIsolationTests
{
    private sealed class MockExternalProcessRunner : IExternalProcessRunner
    {
        public string? LastCommand { get; private set; }
        public IReadOnlyList<string>? LastArguments { get; private set; }
        public bool LastRequireElevation { get; private set; }

        public Task<ExternalProcessResult> RunAsync(
            string command,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken,
            IProgress<string>? standardOutputLines = null,
            TimeSpan? timeout = null,
            bool requireElevation = false)
        {
            LastCommand = command;
            LastArguments = arguments;
            LastRequireElevation = requireElevation;
            return Task.FromResult(new ExternalProcessResult(0, "mock stdout", string.Empty));
        }
    }

    [Fact]
    public async Task ProcessWingetCommandRunner_WriteOperations_AutomaticallyRequireElevation()
    {
        var mockRunner = new MockExternalProcessRunner();
        var runner = new ProcessWingetCommandRunner(mockRunner, new WingetProgressParser());

        // Install operation
        await runner.RunAsync("winget", ["install", "--id", "Git.Git"], CancellationToken.None);
        Assert.True(mockRunner.LastRequireElevation);

        // Uninstall operation
        await runner.RunAsync("winget", ["uninstall", "--id", "Git.Git"], CancellationToken.None);
        Assert.True(mockRunner.LastRequireElevation);

        // Upgrade operation
        await runner.RunAsync("winget", ["upgrade", "--id", "Git.Git"], CancellationToken.None);
        Assert.True(mockRunner.LastRequireElevation);

        // Source add operation
        await runner.RunAsync("winget", ["source", "add", "-n", "test", "-a", "https://example.com"], CancellationToken.None);
        Assert.True(mockRunner.LastRequireElevation);
    }

    [Fact]
    public async Task ProcessWingetCommandRunner_ReadOperations_DoNotRequireElevation()
    {
        var mockRunner = new MockExternalProcessRunner();
        var runner = new ProcessWingetCommandRunner(mockRunner, new WingetProgressParser());

        // Search operation
        await runner.RunAsync("winget", ["search", "Git"], CancellationToken.None);
        Assert.False(mockRunner.LastRequireElevation);

        // List operation
        await runner.RunAsync("winget", ["list"], CancellationToken.None);
        Assert.False(mockRunner.LastRequireElevation);

        // Version operation
        await runner.RunAsync("winget", ["--version"], CancellationToken.None);
        Assert.False(mockRunner.LastRequireElevation);
    }

    [Fact]
    public async Task ProcessExternalProcessRunner_RunsStandardCommandWithoutElevation()
    {
        var runner = new ProcessExternalProcessRunner();
        var result = await runner.RunAsync("cmd.exe", ["/c", "echo test_no_elevation"], CancellationToken.None, requireElevation: false);

        Assert.True(result.Succeeded);
        Assert.Contains("test_no_elevation", result.StandardOutput);
    }
}
