using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Features.Activity;
using OnlyWinget.Features.Home;
using OnlyWinget.Features.Packages;
using OnlyWinget.Features.Settings;
using OnlyWinget.Features.Sources;
using OnlyWinget.Features.Updates;

namespace OnlyWinget.Shell;

public sealed record NavigationRoute(
    string Id,
    string LabelResourceKey,
    Symbol Icon,
    Func<Page> CreatePage,
    bool IsSettings = false);

internal interface INavigationRegistry
{
    IReadOnlyList<NavigationRoute> Routes { get; }
}

internal sealed class NavigationRegistry : INavigationRegistry
{
    public IReadOnlyList<NavigationRoute> Routes { get; } =
    [
        new("home", "Nav_Home", Symbol.Home, static () => new DashboardPage()),
        new("packages", "Nav_Packages", Symbol.Library, static () => new PackagesPage()),
        new("updates", "Nav_Updates", Symbol.Sync, static () => new UpdatesHubPage()),
        new("sources", "Nav_Sources", Symbol.Globe, static () => new SourcesPage()),
        new("activity", "Nav_Activity", Symbol.Clock, static () => new ActivityPage()),
        new("settings", "Nav_Settings", Symbol.Setting, static () => new SettingsPage(), true)
    ];
}
