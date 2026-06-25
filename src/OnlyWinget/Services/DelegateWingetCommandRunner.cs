// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OnlyWinget.Services;

public sealed class DelegateWingetCommandRunner : IWingetCommandRunner
{
    private readonly Func<string?, IReadOnlyList<string>, Action<string>?, CancellationToken, WingetCommandResult> _runner;

    public DelegateWingetCommandRunner(Func<string?, IReadOnlyList<string>, Action<string>?, CancellationToken, WingetCommandResult> runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    public WingetCommandResult Run(
        string? singleArg,
        IReadOnlyList<string> args,
        Action<string>? onOutputLine,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _runner(singleArg, args, onOutputLine, cancellationToken);
    }

    public Task<WingetCommandResult> RunAsync(
        string? singleArg,
        IReadOnlyList<string> args,
        Action<string>? onOutputLine,
        CancellationToken cancellationToken)
    {
        return Task.Run(() => Run(singleArg, args, onOutputLine, cancellationToken), cancellationToken);
    }
}
