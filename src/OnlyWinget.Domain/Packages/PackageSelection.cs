namespace OnlyWinget.Domain.Packages;

public sealed record PackageSelection
{
    public PackageSelection(PackageIdentity package, PackageAction action, string? installedName = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        Package = package;
        Action = action;
        InstalledName = string.IsNullOrWhiteSpace(installedName) ? null : installedName.Trim();
    }

    public PackageIdentity Package { get; }
    public PackageAction Action { get; }

    /// <summary>Installed display name, used to retry an upgrade when winget cannot match the package by id.</summary>
    public string? InstalledName { get; }
}
