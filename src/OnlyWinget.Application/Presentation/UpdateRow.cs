namespace OnlyWinget.Application.Presentation;

public sealed record UpdateRow(
    string PackageId,
    string Name,
    string? Source,
    string InstalledVersion,
    string AvailableVersion,
    string Architecture,
    bool IsSelected,
    string? Status,
    string? ErrorDetails,
    string? Output);
