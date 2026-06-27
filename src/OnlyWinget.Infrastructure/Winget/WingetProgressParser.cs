using System.Text.RegularExpressions;
using OnlyWinget.Application.Winget;

namespace OnlyWinget.Infrastructure.Winget;

public sealed partial class WingetProgressParser
{
    public WingetProgress? Parse(string rawLine)
    {
        if (string.IsNullOrWhiteSpace(rawLine))
        {
            return null;
        }

        var line = AnsiExpression().Replace(rawLine, string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var percentageMatch = PercentageExpression().Match(line);
        int? percentage = percentageMatch.Success && int.TryParse(percentageMatch.Groups[1].Value, out var value)
            ? value
            : null;
        var phase = DownloadExpression().IsMatch(line)
            ? WingetProgressPhase.Downloading
            : InstallExpression().IsMatch(line)
                ? WingetProgressPhase.Installing
                : WingetProgressPhase.Starting;
        return new WingetProgress(phase, percentage, line);
    }

    [GeneratedRegex("\\x1B(?:[@-Z\\\\-_]|\\[[0-?]*[ -/]*[@-~])")]
    private static partial Regex AnsiExpression();

    [GeneratedRegex(@"(?<!\d)(100|\d{1,2})\s*%")]
    private static partial Regex PercentageExpression();

    [GeneratedRegex(@"download|scaric|télécharg|descarg|herunterlad", RegexOptions.IgnoreCase)]
    private static partial Regex DownloadExpression();

    [GeneratedRegex(@"install|installa|instalación|installation", RegexOptions.IgnoreCase)]
    private static partial Regex InstallExpression();
}
