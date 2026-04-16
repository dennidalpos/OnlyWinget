// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System.Collections.Generic;
using System.Threading.Tasks;
using OnlyWinget.Models;

namespace OnlyWinget.Services;

public sealed class WingetQueryService
{
    private readonly WingetService _wingetService;

    public WingetQueryService(WingetService wingetService)
    {
        _wingetService = wingetService;
    }

    public bool TestAvailable() => _wingetService.TestAvailable();

    public string LogDirectory => _wingetService.LogDirectory;

    public Task<IReadOnlyList<SearchResult>> SearchAsync(string query)
    {
        return Task.Run(() => _wingetService.Search(query));
    }

    public Task<IReadOnlyList<UpdateEntry>> LoadUpdatesAsync()
    {
        return Task.Run(() => _wingetService.LoadUpdates());
    }
}
