namespace OnlyWinget.Domain.Packages;

public sealed record PackageSelection
{
    public PackageSelection(PackageIdentity package, PackageAction action)
    {
        ArgumentNullException.ThrowIfNull(package);
        Package = package;
        Action = action;
    }

    public PackageIdentity Package { get; }
    public PackageAction Action { get; }
}
