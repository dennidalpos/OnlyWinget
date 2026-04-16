// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OnlyWinget.Models;

namespace OnlyWinget.Services;

public sealed class UpdatesWorkspaceService
{
    private readonly WingetQueryService _wingetQueryService;
    private readonly IOperationRunner _operationRunner;

    public UpdatesWorkspaceService(WingetQueryService wingetQueryService, IOperationRunner operationRunner)
    {
        _wingetQueryService = wingetQueryService;
        _operationRunner = operationRunner;
    }

    public Task<IReadOnlyList<UpdateEntry>> LoadAsync()
    {
        return _wingetQueryService.LoadUpdatesAsync();
    }

    public async Task<IReadOnlyList<UpdateEntry>> ApplyAsync(
        IReadOnlyList<UpdateEntry> selectedUpdates,
        Action<string, UiStatusState> setStatusById,
        Action<string> appendOutput,
        Action<int, string> reportProgress,
        LocalizedStrings strings,
        Action<string, string, string>? setErrorById = null)
    {
        await _operationRunner.RunUpdatesAsync(selectedUpdates, setStatusById, appendOutput, reportProgress, strings, setErrorById);
        return await LoadAsync();
    }
}
