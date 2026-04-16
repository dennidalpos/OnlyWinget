// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System.Threading;
using System.Threading.Tasks;
using OnlyWinget.Commands;
using Xunit;

namespace OnlyWinget.Tests;

public sealed class AsyncRelayCommandTests
{
    [Fact]
    public async Task Execute_IgnoresConcurrentInvocation()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executions = 0;

        var command = new AsyncRelayCommand(async () =>
        {
            Interlocked.Increment(ref executions);
            started.TrySetResult();
            await release.Task;
        });

        command.Execute(null);
        await started.Task;
        command.Execute(null);

        Assert.Equal(1, Volatile.Read(ref executions));

        release.TrySetResult();
        await Task.Delay(50);
        Assert.Equal(1, Volatile.Read(ref executions));
    }
}
