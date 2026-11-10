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
    int PackagePercentage,
    int CompletedPackages,
    int TotalPackages)
{
    public OperationProgress(
        string packageId,
        WingetProgressPhase phase,
        int percentage,
        int completedPackages,
        int totalPackages)
        : this(packageId, phase, percentage, percentage, completedPackages, totalPackages)
    {
    }
}

