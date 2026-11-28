using OnlyWinget.Application.System;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Infrastructure.System;
using OnlyWinget.Infrastructure.WindowsUpdate;

namespace OnlyWinget.Tests;

public sealed class PowerShellWindowsUpdateJobTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletedOrFailedEndAlwaysCleansUp(bool failEnd)
    {
        if (!OperatingSystem.IsWindows()) { return; }
        var directory = Directory.CreateTempSubdirectory("OnlyWinget-WUA-test-");
        var trace = Path.Combine(directory.FullName, "trace.txt");
        try
        {
            var script = FixtureScript(trace, complete: true, failEnd);
            var result = await CreateService().RunPowerShellAsync(script, CancellationToken.None);
            Assert.True(result.ExitCode == (failEnd ? 1 : 0), result.StandardError);
            Assert.True(File.Exists(trace), result.StandardError);
            Assert.Equal("end,cleanup", await File.ReadAllTextAsync(trace));
            if (!failEnd) { Assert.Contains("completed", result.StandardOutput, StringComparison.Ordinal); }
        }
        finally
        {
            if (File.Exists(trace)) { File.Delete(trace); }
            directory.Delete();
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ActiveCancellationWaitsForCleanupAndReportsNativeCleanupFailure(bool failCleanup, bool timeout)
    {
        if (!OperatingSystem.IsWindows()) { return; }
        var directory = Directory.CreateTempSubdirectory("OnlyWinget-WUA-test-");
        var trace = Path.Combine(directory.FullName, "trace.txt");
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var script = FixtureScript(trace, complete: false, failEnd: false);
            if (failCleanup)
            {
                script = script.Replace("($this.Calls -join ','))", "($this.Calls -join ',')); throw 'fixture cleanup failure'", StringComparison.Ordinal);
            }
            var task = CreateService().RunPowerShellAsync(script,
                cancellation.Token, timeout: TimeSpan.FromSeconds(timeout ? 10 : 60), standardErrorLines: new InlineProgress<string>(line =>
                {
                    if (line == "job-started") { started.TrySetResult(); }
                }));
            await started.Task.WaitAsync(TimeSpan.FromSeconds(20));
            if (!timeout) { cancellation.Cancel(); }
            if (failCleanup)
            {
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
                Assert.Contains("state is uncertain", error.Message, StringComparison.Ordinal);
            }
            else if (timeout)
            {
                await Assert.ThrowsAsync<TimeoutException>(() => task);
            }
            else
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
            }
            Assert.Equal("abort,cleanup", await File.ReadAllTextAsync(trace));
        }
        finally
        {
            cancellation.Cancel();
            if (File.Exists(trace)) { File.Delete(trace); }
            directory.Delete();
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task IncompleteSelectionOrUnsuccessfulSearchCannotAcceptEulaOrStartDownload(int searchResult)
    {
        if (!OperatingSystem.IsWindows()) { return; }
        var fixture = $$"""
$fixtureUpdate = [pscustomobject]@{ Identity = [pscustomobject]@{ UpdateID = 'available'; RevisionNumber = 1 }; Title = 'fixture'; EulaAccepted = $false }
$fixtureUpdate | Add-Member ScriptMethod AcceptEula { throw 'FORBIDDEN EULA MUTATION' }
$fixtureSearcher = [pscustomobject]@{ Result = [pscustomobject]@{ ResultCode = {{searchResult}}; Updates = @($fixtureUpdate) } }
$fixtureSearcher | Add-Member ScriptMethod BeginSearch {
    param($criteria, $callback, $state)
    $fakeJob = [pscustomobject]@{}
    $fakeJob | Add-Member ScriptMethod CleanUp { }
    $callback.Invoke($fakeJob, $null)
    return $fakeJob
}
$fixtureSearcher | Add-Member ScriptMethod EndSearch { param($job) return $this.Result }
$fixtureSession = [pscustomobject]@{ Searcher = $fixtureSearcher }
$fixtureSession | Add-Member ScriptMethod CreateUpdateSearcher { return $this.Searcher }
$fixtureSession | Add-Member ScriptMethod CreateUpdateDownloader { throw 'FORBIDDEN DOWNLOAD' }
function New-Object {
    param([Parameter(Position=0)][string]$TypeName, [string]$ComObject)
    switch ($ComObject) {
        'Microsoft.Update.Session' { return $fixtureSession }
        'Microsoft.Update.UpdateColl' { return ,([Collections.ArrayList]::new()) }
        default { return Microsoft.PowerShell.Utility\New-Object -TypeName $TypeName }
    }
}
""";
        var service = new PowerShellWindowsUpdateService(new FixtureRunner(fixture), new AvailableCapabilities());
        var result = await service.InstallAsync(
            [new WindowsUpdateIdentity("available", 1), new WindowsUpdateIdentity("available", 2)], new(), CancellationToken.None);
        Assert.False(result.Succeeded);
        Assert.DoesNotContain("FORBIDDEN", result.Error?.Message ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains(searchResult == 2 ? "not found" : "search did not complete", result.Error?.Message ?? string.Empty, StringComparison.Ordinal);
    }

    private sealed class AvailableCapabilities : ISystemCapabilityService
    {
        public Task<SystemCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new SystemCapabilities(true, true, true, true, null));
    }

    private sealed class FixtureRunner(string fixture) : IExternalProcessRunner
    {
        public Task<ExternalProcessResult> RunAsync(string command, IReadOnlyList<string> arguments, CancellationToken cancellationToken,
            IProgress<string>? standardOutputLines = null, TimeSpan? timeout = null, IProgress<string>? standardErrorLines = null)
        {
            var script = global::System.Text.Encoding.Unicode.GetString(Convert.FromBase64String(arguments[^1]));
            var fixtureArguments = arguments.ToArray();
            fixtureArguments[^1] = Convert.ToBase64String(global::System.Text.Encoding.Unicode.GetBytes(fixture + "\n" + script));
            return new ProcessExternalProcessRunner().RunAsync(command, fixtureArguments, cancellationToken, standardOutputLines, timeout, standardErrorLines);
        }
    }

    private static PowerShellWindowsUpdateService CreateService() =>
        new(new ProcessExternalProcessRunner(), new UnusedCapabilities());

    private sealed class UnusedCapabilities : ISystemCapabilityService
    {
        public Task<SystemCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The worker fixture must not probe or modify the machine.");
    }

    private static string FixtureScript(string trace, bool complete, bool failEnd) => $$"""
$fixtureJob = [pscustomobject]@{ Trace = '{{trace.Replace("'", "''", StringComparison.Ordinal)}}'; Calls = (New-Object 'Collections.Generic.List[string]') }
$fixtureJob | Add-Member ScriptMethod RequestAbort { $this.Calls.Add('abort') }
$fixtureJob | Add-Member ScriptMethod CleanUp { $this.Calls.Add('cleanup'); [IO.File]::WriteAllText($this.Trace, ($this.Calls -join ',')) }
Invoke-OnlyWingetUpdateJob -Begin {
    param($callback)
    if (${{complete}}) { $callback.Invoke($fixtureJob, $null) }
    [Console]::Error.WriteLine('job-started')
    $fixtureJob
} -End {
    param($value)
    $value.Calls.Add('end')
    if (${{failEnd}}) { throw 'fixture End failure' }
    'completed'
}
""";
}
