using OnlyWinget.Domain.Presets;

namespace OnlyWinget.Application.Presets;

public sealed record OnlyWingetPresetDocument(string Format, Preset Preset)
{
    public const string CurrentFormat = "onlywinget.preset.v1";

    public static OnlyWingetPresetDocument Create(Preset preset) => new(CurrentFormat, preset);
}
