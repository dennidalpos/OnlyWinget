// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OnlyWinget.Services;

public interface IWingetCommandRunner
{
    WingetCommandResult Run(
        string? singleArg,
        IReadOnlyList<string> args,
        Action<string>? onOutputLine,
        CancellationToken cancellationToken);

    Task<WingetCommandResult> RunAsync(
        string? singleArg,
        IReadOnlyList<string> args,
        Action<string>? onOutputLine,
        CancellationToken cancellationToken);
}
