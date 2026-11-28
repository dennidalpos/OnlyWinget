using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Application.Winget;

public sealed record PackageUpdate(
    PackageIdentity Package,
    string Name,
    string InstalledVersion,
    string AvailableVersion);
