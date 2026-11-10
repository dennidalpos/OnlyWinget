using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Application.Winget;

public sealed record PackageResolution(
    PackageIdentity Package,
    string? Name,
    string? Version,
    string? Publisher,
    bool IsResolved,
    ClassifiedWingetError? Error,
    IReadOnlyList<string>? Architectures = null);
