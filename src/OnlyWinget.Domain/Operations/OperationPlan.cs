using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Domain.Operations;

public sealed record OperationPlan
{
    public OperationPlan(string name, IReadOnlyList<PackageSelection> selections)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Plan name is required.", nameof(name));
        }
        ArgumentNullException.ThrowIfNull(selections);

        Name = name.Trim();
        Selections = selections.ToArray();
    }

    public string Name { get; }

    public IReadOnlyList<PackageSelection> Selections { get; }

    public bool HasWork => Selections.Count > 0;
}
