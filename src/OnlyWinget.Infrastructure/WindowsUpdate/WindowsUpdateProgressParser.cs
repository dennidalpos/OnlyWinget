using OnlyWinget.Application.Winget;

namespace OnlyWinget.Infrastructure.WindowsUpdate;

/// <summary>
/// Parses the "##OWU-PROGRESS##Phase##Percent" marker lines that PowerShellWindowsUpdateService.InstallScript
/// writes to stderr while polling the WUA download/install jobs, so real progress can be reported instead of
/// hard-coded percentages.
/// </summary>
public static class WindowsUpdateProgressParser
{
    public const string MarkerPrefix = "##OWU-PROGRESS##";

    public static OperationProgress? Parse(string rawLine, int totalUpdates)
    {
        if (string.IsNullOrEmpty(rawLine) || !rawLine.StartsWith(MarkerPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var parts = rawLine[MarkerPrefix.Length..].Split("##");
        if (parts.Length != 2 || !int.TryParse(parts[1], out var percent))
        {
            return null;
        }

        WingetProgressPhase phase;
        switch (parts[0])
        {
            case "Downloading":
                phase = WingetProgressPhase.Downloading;
                break;
            case "Installing":
                phase = WingetProgressPhase.Installing;
                break;
            default:
                return null;
        }

        return new OperationProgress("WindowsUpdate", phase, Math.Clamp(percent, 0, 100), 0, totalUpdates);
    }

    public static string StripMarkerLines(string text) =>
        string.IsNullOrEmpty(text)
            ? text
            : string.Join(
                Environment.NewLine,
                text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Where(line => !line.StartsWith(MarkerPrefix, StringComparison.Ordinal)));
}
