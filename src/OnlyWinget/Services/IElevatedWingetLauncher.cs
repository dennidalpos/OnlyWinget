// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;
using System.Threading;
using OnlyWinget.Models;

namespace OnlyWinget.Services;

public interface IElevatedWingetLauncher
{
    WingetCommandResult Launch(
        IReadOnlyList<string> args,
        string? logFilePath,
        Action<string>? onOutputLine = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}
