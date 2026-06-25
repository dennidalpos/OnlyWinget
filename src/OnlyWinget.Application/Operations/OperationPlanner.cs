using OnlyWinget.Domain.Operations;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Presets;

namespace OnlyWinget.Application.Operations;

public sealed class OperationPlanner
{
    public OperationPlan CreatePresetPlan(Preset preset, PackageAction action)
    {
        ArgumentNullException.ThrowIfNull(preset);

        var selections = preset.Packages
            .Select(package => new PackageSelection(package, action))
            .ToArray();

        return new OperationPlan(preset.Name, selections);
    }
}
