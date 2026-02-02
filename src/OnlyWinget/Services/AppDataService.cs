using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using OnlyWinget.Models;

namespace OnlyWinget.Services;

public sealed class AppDataService
{
    public (Dictionary<string, List<AppEntry>> Tabs, List<string> TabNames) Load(string jsonPath)
    {
        var tabs = new Dictionary<string, List<AppEntry>>(StringComparer.OrdinalIgnoreCase);
        var tabNames = new List<string>();

        if (!File.Exists(jsonPath))
        {
            CreateDefaultTab(tabs, tabNames);
            return (tabs, tabNames);
        }

        try
        {
            var rawText = File.ReadAllText(jsonPath);
            if (string.IsNullOrWhiteSpace(rawText))
            {
                CreateDefaultTab(tabs, tabNames);
                return (tabs, tabNames);
            }

            var trimmed = rawText.TrimStart();
            if (trimmed.StartsWith("{", StringComparison.Ordinal))
            {
                var root = JsonSerializer.Deserialize<AppDataRoot>(rawText, JsonOptions());
                if (root?.Tabs != null)
                {
                    foreach (var tab in root.Tabs)
                    {
                        var list = new List<AppEntry>();
                        if (tab.Apps != null)
                        {
                            foreach (var app in tab.Apps)
                            {
                                list.Add(new AppEntry
                                {
                                    Name = app.Name,
                                    Id = app.Id,
                                    Action = NormalizeAction(app.Action),
                                    Status = string.Empty
                                });
                            }
                        }

                        if (!tabs.ContainsKey(tab.Name))
                        {
                            tabs[tab.Name] = list;
                            tabNames.Add(tab.Name);
                        }
                    }
                }
            }
            else
            {
                var listData = JsonSerializer.Deserialize<List<AppDataItem>>(rawText, JsonOptions());
                var list = new List<AppEntry>();
                if (listData != null)
                {
                    foreach (var app in listData)
                    {
                        list.Add(new AppEntry
                        {
                            Name = app.Name,
                            Id = app.Id,
                            Action = NormalizeAction(app.Action),
                            Status = string.Empty
                        });
                    }
                }

                tabs["Default"] = list;
                tabNames.Add("Default");
            }
        }
        catch
        {
            CreateDefaultTab(tabs, tabNames);
        }

        if (tabs.Count == 0)
        {
            CreateDefaultTab(tabs, tabNames);
        }

        return (tabs, tabNames);
    }

    public bool Save(string jsonPath, IReadOnlyList<string> tabNames, Dictionary<string, List<AppEntry>> tabs)
    {
        try
        {
            var root = new AppDataRoot();
            foreach (var tabName in tabNames)
            {
                if (!tabs.TryGetValue(tabName, out var apps))
                {
                    continue;
                }

                var tab = new AppTabData
                {
                    Name = tabName,
                    Apps = new List<AppDataItem>()
                };

                foreach (var app in apps)
                {
                    tab.Apps.Add(new AppDataItem
                    {
                        Name = app.Name,
                        Id = app.Id,
                        Action = app.Action
                    });
                }

                root.Tabs.Add(tab);
            }

            var json = JsonSerializer.Serialize(root, JsonOptions());
            File.WriteAllText(jsonPath, json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string NormalizeAction(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return "Install";
        }

        return action switch
        {
            "Install" => "Install",
            "Uninstall" => "Uninstall",
            "Pause" => "Pause",
            _ => "Install"
        };
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static void CreateDefaultTab(Dictionary<string, List<AppEntry>> tabs, List<string> tabNames)
    {
        tabs["Default"] = new List<AppEntry>();
        tabNames.Add("Default");
    }
}
