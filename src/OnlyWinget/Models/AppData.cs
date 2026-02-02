using System.Collections.Generic;

namespace OnlyWinget.Models;

public sealed class AppDataRoot
{
    public List<AppTabData> Tabs { get; set; } = new();
}

public sealed class AppTabData
{
    public string Name { get; set; } = string.Empty;
    public List<AppDataItem> Apps { get; set; } = new();
}

public sealed class AppDataItem
{
    public string Name { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
}
