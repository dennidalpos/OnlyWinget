using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Domain.Operations;

public sealed record OperationPlan(
    string Name,
    IReadOnlyList<PackageSelection> Selections)
{
    public bool HasWork => Selections.Count > 0;
}
