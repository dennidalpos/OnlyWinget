// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using OnlyWinget.Models;

namespace OnlyWinget.Services;

public sealed class PresetWorkspaceService
{
    private readonly AppDataService _dataService;

    public PresetWorkspaceService(AppDataService dataService)
    {
        _dataService = dataService;
    }

    public AppDataLoadResult Load()
    {
        var jsonPath = _dataService.GetJsonPath();
        return _dataService.Load(jsonPath);
    }

    public SaveResult Save(IReadOnlyList<string> tabNames, IReadOnlyDictionary<string, ObservableCollection<AppEntry>> tabs)
    {
        var jsonPath = _dataService.GetJsonPath();
        var snapshot = tabs.ToDictionary(pair => pair.Key, pair => pair.Value.ToList(), System.StringComparer.OrdinalIgnoreCase);
        return _dataService.Save(jsonPath, tabNames, snapshot);
    }

    public PresetImportResult ImportPreset(string path, IReadOnlyCollection<string> existingPresetNames)
    {
        return _dataService.ImportPreset(path, existingPresetNames);
    }

    public SaveResult ExportPreset(string path, string presetName, IEnumerable<AppEntry> apps)
    {
        return _dataService.ExportPreset(path, presetName, apps);
    }

    public string GetDefaultPresetExportFileName(string presetName)
    {
        return _dataService.GetDefaultPresetExportFileName(presetName);
    }
}
