namespace OnlyWinget.Domain.Packages;

public sealed record PackageSelection(PackageIdentity Package, PackageAction Action);
