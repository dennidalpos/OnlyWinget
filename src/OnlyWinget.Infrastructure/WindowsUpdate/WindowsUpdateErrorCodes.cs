namespace OnlyWinget.Infrastructure.WindowsUpdate;

/// <summary>
/// Maps Windows Update Agent (WUA) WU_E_* HRESULT codes to short, human-readable messages.
/// Source: https://learn.microsoft.com/windows/win32/wua_sdk/wua-success-and-error-codes-
/// </summary>
public static class WindowsUpdateErrorCodes
{
    private static readonly IReadOnlyDictionary<int, string> Descriptions = new Dictionary<int, string>
    {
        [unchecked((int)0x80240001)] = "Windows Update service was unable to provide the service.",
        [unchecked((int)0x80240009)] = "Another Windows Update operation was already in progress.",
        [unchecked((int)0x8024000B)] = "The operation was cancelled.",
        [unchecked((int)0x80240016)] = "Another installation was in progress, or the system is pending a mandatory restart.",
        [unchecked((int)0x80240017)] = "No applicable updates were found.",
        [unchecked((int)0x8024001D)] = "The update contains invalid metadata.",
        [unchecked((int)0x8024001E)] = "The operation did not complete because the service or system was shutting down.",
        [unchecked((int)0x8024001F)] = "The operation did not complete because the network connection was unavailable.",
        [unchecked((int)0x80240021)] = "The operation timed out.",
        [unchecked((int)0x80240022)] = "The operation failed for all the updates.",
        [unchecked((int)0x80240023)] = "The license terms for the update were declined.",
        [unchecked((int)0x80240024)] = "There are no updates.",
        [unchecked((int)0x80240025)] = "Group Policy settings prevented access to Windows Update.",
        [unchecked((int)0x80240034)] = "The update failed to download.",
    };

    /// <summary>
    /// Returns a human-readable message for a WUA HRESULT, or null when it represents success (0).
    /// Unknown non-zero codes fall back to the raw hexadecimal HRESULT so no diagnostic detail is lost.
    /// </summary>
    public static string? Describe(int hResult)
    {
        if (hResult == 0)
        {
            return null;
        }

        var hex = $"0x{unchecked((uint)hResult):X8}";
        return Descriptions.TryGetValue(hResult, out var description)
            ? $"{description} ({hex})"
            : $"HRESULT {hex}";
    }
}
