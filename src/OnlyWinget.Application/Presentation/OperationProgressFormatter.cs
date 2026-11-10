using OnlyWinget.Application.Winget;

namespace OnlyWinget.Application.Presentation;

public static class OperationProgressFormatter
{
    public static string FormatMessage(OperationProgress? progress, Func<string, string> textResolver)
    {
        ArgumentNullException.ThrowIfNull(textResolver);

        if (progress is null)
        {
            return textResolver("Progress_Starting");
        }

        var phaseKey = $"Progress_{progress.Phase}";
        var phaseText = textResolver(phaseKey);

        if (string.IsNullOrWhiteSpace(progress.PackageId))
        {
            return phaseText;
        }

        var total = progress.TotalPackages;
        var current = CalculateCurrentPackageIndex(progress);

        return total > 0
            ? $"{phaseText} ({current}/{total}): {progress.PackageId}"
            : $"{phaseText}: {progress.PackageId}";
    }

    public static string FormatProgressText(OperationProgress? progress, Func<string, string> textResolver)
    {
        ArgumentNullException.ThrowIfNull(textResolver);

        if (progress is null)
        {
            return textResolver("Progress_Starting");
        }

        var phaseKey = $"Progress_{progress.Phase}";
        var phaseText = textResolver(phaseKey);

        if (string.IsNullOrWhiteSpace(progress.PackageId))
        {
            return phaseText;
        }

        var total = progress.TotalPackages;
        var current = CalculateCurrentPackageIndex(progress);
        var countSuffix = total > 0 ? $" ({current}/{total})" : string.Empty;
        var percentage = Math.Clamp(progress.Percentage, 0, 100);

        return $"{phaseText}{countSuffix} · {percentage}% · {progress.PackageId}";
    }

    private static int CalculateCurrentPackageIndex(OperationProgress progress)
    {
        var total = progress.TotalPackages;
        if (total <= 0) return 0;
        if (progress.Phase == WingetProgressPhase.Completed || progress.CompletedPackages >= total)
        {
            return total;
        }
        return Math.Clamp(progress.CompletedPackages + 1, 1, total);
    }
}
