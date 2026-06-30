using System.Text.Json.Serialization;

namespace OnlyWinget.Domain.Packages;

public sealed record PackageIdentity
{
    [JsonConstructor]
    public PackageIdentity(string id, string? source = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Package id is required.", nameof(id));
        }

        Id = id.Trim();
        Source = string.IsNullOrWhiteSpace(source) ? null : source.Trim();
    }

    public string Id { get; }

    public string? Source { get; }
}
