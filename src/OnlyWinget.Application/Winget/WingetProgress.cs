namespace OnlyWinget.Application.Winget;

public enum WingetProgressPhase
{
    Starting,
    Downloading,
    Installing,
    Completed,
    Failed
}

public sealed record WingetProgress(WingetProgressPhase Phase, int? Percentage, string? Message);

public sealed record OperationProgress(
    string PackageId,
    WingetProgressPhase Phase,
    int Percentage,
    int CompletedPackages,
    int TotalPackages);
