namespace OnlyWinget.Application.Presentation;

public sealed record UpdateRow(
    string PackageId,
    string Name,
    string? Source,
    string InstalledVersion,
    string AvailableVersion,
    bool IsSelected);
