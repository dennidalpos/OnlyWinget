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

        var current = Math.Max(1, progress.CompletedPackages + 1);
        var total = progress.TotalPackages;

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

        var current = Math.Max(1, progress.CompletedPackages + 1);
        var total = progress.TotalPackages;
        var countSuffix = total > 0 ? $" ({current}/{total})" : string.Empty;

        return $"{phaseText}{countSuffix} · {progress.Percentage}% · {progress.PackageId}";
    }
}
