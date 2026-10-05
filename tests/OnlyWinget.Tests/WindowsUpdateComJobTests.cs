using OnlyWinget.Infrastructure.WindowsUpdate;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace OnlyWinget.Tests;

public sealed class WindowsUpdateComJobTests
{
    [Fact]
    [Trait("Category", "Smoke")]
    [SupportedOSPlatform("windows")]
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("This live COM smoke test runs in the untrimmed test host and binds to OS-provided automation metadata.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("This live COM smoke test uses the Windows automation runtime binder.")]
    public async Task NativeSearchCallbackCompletesOrAbortsWithoutFallback()
    {
        if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("ONLYWINGET_RUN_WINGET_SMOKE") != "1")
        {
            return;
        }

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var aborted = false;
        var cleaned = false;
        await Task.Run(async () =>
        {
            object session = Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.Session")!)!;
            object searcher = ((dynamic)session).CreateUpdateSearcher();
            try
            {
                try
                {
                    var result = await WindowsUpdateComJob.RunAsync(
                        callback => ((dynamic)searcher).BeginSearch("IsInstalled=0 and IsHidden=0", callback, null),
                        job => ((dynamic)searcher).EndSearch((dynamic)job),
                        job => { aborted = true; ((dynamic)job).RequestAbort(); },
                        job => { ((dynamic)job).CleanUp(); Marshal.ReleaseComObject(job); cleaned = true; }, cancellation.Token);
                    Assert.NotNull(result);
                    Marshal.ReleaseComObject(result);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    Assert.True(aborted);
                }
                Assert.True(cleaned);
            }
            finally
            {
                Marshal.ReleaseComObject(searcher);
                Marshal.ReleaseComObject(session);
            }
        });
    }

    [Fact]
    public async Task CompletionBeforeBeginReturnsStillEndsAndCleansUpTheJob()
    {
        var expected = new object();
        var ended = false;
        var cleaned = false;
        var result = await WindowsUpdateComJob.RunAsync(callback =>
        {
            ((IWindowsUpdateCompletionCallback)callback).Invoke(expected, new object());
            return expected;
        }, job => { Assert.Same(expected, job); ended = true; return expected; },
        _ => throw new InvalidOperationException("A completed job must not be aborted."),
        _ => cleaned = true, CancellationToken.None);

        Assert.Same(expected, result);
        Assert.True(ended);
        Assert.True(cleaned);
    }

    [Fact]
    public async Task CancellationOfAnActiveJobRequestsAbortAndCleansUpWithoutEndingOrRetrying()
    {
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var aborts = 0;
        var cleanups = 0;
        var ends = 0;
        var task = WindowsUpdateComJob.RunAsync(_ =>
        {
            started.SetResult();
            return new object();
        }, job => { ends++; return job; }, _ => aborts++, _ => cleanups++, cancellation.Token);
        await started.Task;
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(1, aborts);
        Assert.Equal(1, cleanups);
        Assert.Equal(0, ends);
    }

    [Fact]
    public async Task AlreadyCancelledWorkDoesNotStartAJob()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var starts = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WindowsUpdateComJob.RunAsync(
            _ => { starts++; return new object(); }, job => job, _ => { }, _ => { }, cancellation.Token));

        Assert.Equal(0, starts);
    }

    [Fact]
    public async Task FailedEndStillCleansUpTheJob()
    {
        var cleaned = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => WindowsUpdateComJob.RunAsync(callback =>
        {
            var job = new object();
            ((IWindowsUpdateCompletionCallback)callback).Invoke(job, new object());
            return job;
        }, _ => throw new InvalidOperationException("End failure"), _ => { }, _ => cleaned = true, CancellationToken.None));

        Assert.True(cleaned);
    }
}
