using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Domain.Presets;

public sealed record Preset
{
    public Preset(string name, IReadOnlyList<PackageIdentity> packages)
    {
        Name = NormalizeName(name);
        ArgumentNullException.ThrowIfNull(packages);
        Packages = packages.ToArray();
    }

    public string Name { get; }

    public IReadOnlyList<PackageIdentity> Packages { get; }

    public bool Equals(Preset? other) =>
        other is not null &&
        string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(Name);

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Preset name is required.", nameof(name));
        }

        return name.Trim();
    }
}
