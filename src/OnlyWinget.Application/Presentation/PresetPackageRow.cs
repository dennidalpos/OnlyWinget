namespace OnlyWinget.Application.Presentation;

public sealed record PresetPackageRow(
    string PackageId,
    string? Source,
    string? Version,
    string Architecture,
    bool IsSelected);
