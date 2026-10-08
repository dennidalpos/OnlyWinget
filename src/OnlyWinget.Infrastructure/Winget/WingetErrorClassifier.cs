using OnlyWinget.Application.Winget;

namespace OnlyWinget.Infrastructure.Winget;

public sealed class WingetErrorClassifier
{
    // Native codes take precedence over localized output; see Microsoft's returnCodes.md.
    private static readonly IReadOnlyDictionary<int, WingetErrorKind> KnownExitCodes = new Dictionary<int, WingetErrorKind>
    {
        [unchecked((int)0x8A150002)] = WingetErrorKind.Unknown, // Invalid CLI arguments.
        [unchecked((int)0x8A150005)] = WingetErrorKind.Cancelled, // Ctrl signal.
        [unchecked((int)0x8A15000F)] = WingetErrorKind.SourceUnavailable, // Missing source data.
        [unchecked((int)0x8A150011)] = WingetErrorKind.HashMismatch,
        [unchecked((int)0x8A150012)] = WingetErrorKind.SourceUnavailable, // Unknown source name.
        [unchecked((int)0x8A150014)] = WingetErrorKind.NotFound,
        [unchecked((int)0x8A150015)] = WingetErrorKind.SourceUnavailable, // No configured sources.
        [unchecked((int)0x8A150016)] = WingetErrorKind.Unknown, // Ambiguous package matches.
        [unchecked((int)0x8A150017)] = WingetErrorKind.NotFound, // Missing manifest.
        [unchecked((int)0x800704C7)] = WingetErrorKind.Cancelled,
        [unchecked((int)0x8A15002B)] = WingetErrorKind.NoUpdates,
        [unchecked((int)0x8A150045)] = WingetErrorKind.SourceUnavailable, // Source open failed.
        [unchecked((int)0x8A15005E)] = WingetErrorKind.SourceUnavailable, // Pinned certificate mismatch.
        [unchecked((int)0x8A15006A)] = WingetErrorKind.Cancelled, // Shutdown signal.
        [unchecked((int)0x8A150077)] = WingetErrorKind.Cancelled, // Authentication cancelled.
        [unchecked((int)0x8A15010C)] = WingetErrorKind.Cancelled, // Installer cancelled.
        [unchecked((int)0x8A150114)] = WingetErrorKind.CannotUpgrade,
        [unchecked((int)0x8A150115)] = WingetErrorKind.Unknown, // Custom installer error.
    };

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

        var knownCode = KnownExitCodes.TryGetValue(result.ExitCode, out var kind);
        if (!knownCode)
        {
            kind = WingetErrorKind.Unknown;
        }

        if (!knownCode)
        {
            if (ContainsAny(
                text,
                "No applicable update found",
                "No available upgrade found",
                "Nessun aggiornamento disponibile",
                "Nessun aggiornamento applicabile",
                "Non è stato trovato alcun aggiornamento applicabile",
                "non si applica al sistema o ai requisiti",
                "does not apply to the system or requirements",
                "No applicable update was found",
                "è necessario un targeting esplicito"))
            {
                kind = WingetErrorKind.NoUpdates;
            }
            else if (ContainsAny(
                text,
                "No package found",
                "No installed package found",
                "Non è stato trovato alcun pacchetto installato corrispondente ai criteri di input",
                "Nessun pacchetto installato corrispondente",
                "No package found matching input criteria",
                "Nessun pacchetto trovato con criteri di input corrispondenti",
                "Nessun pacchetto trovato",
                "Nessun pacchetto installato trovato"))
            {
                kind = WingetErrorKind.NotFound;
            }
            else if (ContainsAny(
                text,
                "Failed when searching source",
                "Failed when opening source",
                "0x8a15005e",
                "source agreements",
                "source is not configured",
                "No sources are configured",
                "origine non configurata",
                "origini non sono configurate",
                "contratti dell'origine"))
            {
                kind = WingetErrorKind.SourceUnavailable;
            }
            else if (ContainsAny(text, "cancelled", "canceled", "operation was canceled", "annullata", "annullato"))
            {
                kind = WingetErrorKind.Cancelled;
            }
            else if (ContainsAny(
                text,
                "0x8a150114",
                "Non è possibile aggiornare il pacchetto con WinGet",
                "Package cannot be upgraded with WinGet",
                "Utilizzare il metodo fornito dall'autore",
                "Use provider's method to upgrade",
                "Utilizzare il metodo fornito dal provider"))
            {
                kind = WingetErrorKind.CannotUpgrade;
            }
            else if (ContainsAny(
                text,
                "0x8a150011",
                "-1978335215",
                "InstallerHashOverride",
                "InstallerHashMismatch",
                "ignore-security-hash",
                "controllo hash del programma di installazione",
                "hash del programma di installazione non corrisponde",
                "installer hash does not match"))
            {
                kind = WingetErrorKind.HashMismatch;
            }
        }

        var cleanedText = CleanWingetOutput(text);
        var message = string.IsNullOrWhiteSpace(cleanedText) ? "winget failed." : cleanedText;
        return new ClassifiedWingetError(kind, message);
    }

    public bool IsRetryable(ClassifiedWingetError? error)
    {
        if (error is null)
        {
            return false;
        }

        return error.Kind switch
        {
            WingetErrorKind.NotFound => false,
            WingetErrorKind.NoUpdates => false,
            WingetErrorKind.Cancelled => false,
            WingetErrorKind.CannotUpgrade => false,
            WingetErrorKind.HashMismatch => false,
            _ => true
        };
    }

    internal bool IsRetryable(WingetCommandResult result, ClassifiedWingetError? error) =>
        result.ExitCode is not (unchecked((int)0x8A150002) or unchecked((int)0x8A15000F) or
            unchecked((int)0x8A150012) or unchecked((int)0x8A150015) or unchecked((int)0x8A150016) or
            unchecked((int)0x8A15005E) or unchecked((int)0x8A150115)) && IsRetryable(error);

    private static string CleanWingetOutput(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var usageIndex = text.IndexOf("utilizzo: winget", StringComparison.OrdinalIgnoreCase);
        if (usageIndex < 0)
        {
            usageIndex = text.IndexOf("usage: winget", StringComparison.OrdinalIgnoreCase);
        }

        if (usageIndex >= 0)
        {
            text = text[..usageIndex];
        }

        return text.Trim();
    }

    private static bool ContainsAny(string text, params string[] needles) =>
        needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));
}
