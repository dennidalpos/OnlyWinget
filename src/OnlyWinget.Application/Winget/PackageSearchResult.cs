using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Application.Winget;

public sealed record PackageSearchResult(
    PackageIdentity Package,
    string Name,
    string? Version,
    string? Match);
