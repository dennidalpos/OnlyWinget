// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OnlyWinget.Models;

namespace OnlyWinget.Services;

public interface IOperationRunner
{
    Task RunApplyAsync(
        IReadOnlyList<AppEntry> apps,
        Action<string, UiStatusState> setStatusById,
        Action<string> appendOutput,
        Action<int, string> reportProgress,
        LocalizedStrings strings,
        Action<string, string, string>? setErrorById = null,
        CancellationToken cancellationToken = default);

    Task RunUpdatesAsync(
        IReadOnlyList<UpdateEntry> updates,
        Action<string, UiStatusState> setStatusById,
        Action<string> appendOutput,
        Action<int, string> reportProgress,
        LocalizedStrings strings,
        Action<string, string, string>? setErrorById = null,
        CancellationToken cancellationToken = default);
}
