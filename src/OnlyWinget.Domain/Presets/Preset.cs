using OnlyWinget.Domain.Packages;
using System.Text.Json.Serialization;

namespace OnlyWinget.Domain.Presets;

public sealed record Preset
{
    [JsonConstructor]
    public Preset(string name, IReadOnlyList<PackageIdentity> packages)
    {
        Name = NormalizeName(name);
        Packages = packages.ToArray();
    }

    public string Name { get; }

    public IReadOnlyList<PackageIdentity> Packages { get; }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Preset name is required.", nameof(name));
        }

        return name.Trim();
    }
}
