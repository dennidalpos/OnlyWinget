using OnlyWinget.Application.Winget;

namespace OnlyWinget.Infrastructure.Winget;

public sealed class WingetErrorClassifier
{
    // winget's exit codes are HRESULT-style values baked into the CLI itself, so unlike its message text
    // they do not change with the system display language. Verified live against winget v1.29.280 (it-IT)
    // on 2026-08-19 by triggering each scenario directly (see PROJECT_STATUS.json for the exact commands
    // and captured output). Checked before the text heuristics below so non-EN/IT locales get a correct
    // classification instead of falling into Unknown; unrecognized codes fall through to text matching.
    private static readonly IReadOnlyDictionary<int, WingetErrorKind> KnownExitCodes = new Dictionary<int, WingetErrorKind>
    {
        // 0x8A150002: WINGET_INST_HASH_MISMATCH
        [unchecked((int)0x8A150002)] = WingetErrorKind.HashMismatch,
        // 0x8A150014: WINGET_INST_NO_APPLICABLE_PACKAGE / WINGET_INST_NO_SOURCES_DEFINED
        [unchecked((int)0x8A150014)] = WingetErrorKind.NotFound,
        // 0x8A150015: WINGET_INST_OPERATION_CANCELLED
        [unchecked((int)0x8A150015)] = WingetErrorKind.Cancelled,
        // 0x800704C7: ERROR_CANCELLED (user cancelled operation / UAC prompt)
        [unchecked((int)0x800704C7)] = WingetErrorKind.Cancelled,
        // 0x8A15002B: WINGET_INST_NO_UPGRADE_AVAILABLE
        [unchecked((int)0x8A15002B)] = WingetErrorKind.NoUpdates,
        // 0x8A15005E: WINGET_INST_SOURCE_UNAVAILABLE / WINGET_INST_SOURCE_DATA_MISSING
        [unchecked((int)0x8A15005E)] = WingetErrorKind.SourceUnavailable,
        // 0x8A150114 - 0x8A150117: WINGET_INST_CANNOT_UPGRADE family
        [unchecked((int)0x8A150114)] = WingetErrorKind.CannotUpgrade,
        [unchecked((int)0x8A150115)] = WingetErrorKind.CannotUpgrade,
        [unchecked((int)0x8A150116)] = WingetErrorKind.CannotUpgrade,
        [unchecked((int)0x8A150117)] = WingetErrorKind.CannotUpgrade,
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

        if (!KnownExitCodes.TryGetValue(result.ExitCode, out var kind))
        {
            kind = WingetErrorKind.Unknown;
        }

        if (kind == WingetErrorKind.Unknown)
        {
            if (ContainsAny(
                text,
                "No installed package found matching input criteria",
                "No applicable update found",
                "No available upgrade found",
                "Nessun aggiornamento disponibile",
                "Nessun aggiornamento applicabile",
                "Non è stato trovato alcun aggiornamento applicabile",
                "Non è stato trovato alcun pacchetto installato corrispondente ai criteri di input",
                "Nessun pacchetto installato corrispondente",
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
                "0x8a150115",
                "0x8a150116",
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
                "0x8a150002",
                "-1978335230",
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
