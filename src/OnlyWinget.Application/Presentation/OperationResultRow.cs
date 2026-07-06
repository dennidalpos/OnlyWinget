using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Application.Presentation;

public sealed record OperationResultRow(
    string PackageId,
    string? Source,
    PackageAction Action,
    bool Succeeded,
    bool IsWarning,
    string Status,
    string? ErrorDetails,
    string? Output);
