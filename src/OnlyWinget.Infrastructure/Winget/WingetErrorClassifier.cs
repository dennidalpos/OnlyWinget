using OnlyWinget.Application.Winget;

namespace OnlyWinget.Infrastructure.Winget;

public sealed class WingetErrorClassifier
{
    public ClassifiedWingetError? Classify(WingetCommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Succeeded)
        {
            return null;
        }

        var text = string.Join(
            Environment.NewLine,
            result.StandardOutput,
            result.StandardError);

        var kind = WingetErrorKind.Unknown;
        if (ContainsAny(text, "No installed package found matching input criteria", "No applicable update found", "No available upgrade found"))
        {
            kind = WingetErrorKind.NoUpdates;
        }
        else if (ContainsAny(text, "No package found", "No installed package found", "No package found matching input criteria"))
        {
            kind = WingetErrorKind.NotFound;
        }
        else if (ContainsAny(text, "Failed when searching source", "source agreements", "source is not configured", "No sources are configured"))
        {
            kind = WingetErrorKind.SourceUnavailable;
        }
        else if (ContainsAny(text, "cancelled", "canceled", "operation was canceled"))
        {
            kind = WingetErrorKind.Cancelled;
        }

        var message = string.IsNullOrWhiteSpace(text) ? "winget failed." : text.Trim();
        return new ClassifiedWingetError(kind, message);
    }

    private static bool ContainsAny(string text, params string[] needles) =>
        needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));
}
