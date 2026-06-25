namespace OnlyWinget.Application.Presentation;

public sealed record PresetPackageRow(
    string PackageId,
    string? Source,
    bool IsSelected);
