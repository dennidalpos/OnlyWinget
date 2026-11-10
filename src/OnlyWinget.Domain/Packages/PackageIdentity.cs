namespace OnlyWinget.Domain.Packages;

public sealed record PackageIdentity
{
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

    public bool Equals(PackageIdentity? other) =>
        other is not null &&
        string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Source ?? string.Empty, other.Source ?? string.Empty, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() =>
        HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(Id),
            StringComparer.OrdinalIgnoreCase.GetHashCode(Source ?? string.Empty));
}

