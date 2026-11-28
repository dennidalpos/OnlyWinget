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
        // Verified against "WUA Success and Error Codes" (Microsoft Learn).
        [unchecked((int)0x8024000A)] = "Cancellation of the operation was not allowed.",
        [unchecked((int)0x8024000C)] = "No operation was required.",
        [unchecked((int)0x80240014)] = "The updates requested for installation are not installable by the caller.",
        [unchecked((int)0x80240019)] = "An exclusive update cannot be installed with other updates at the same time.",
        [unchecked((int)0x8024001B)] = "Windows Update Agent is updating itself; try again shortly.",
        [unchecked((int)0x80240020)] = "There is no logged-on interactive user.",
        [unchecked((int)0x80240028)] = "The update could not be uninstalled because the request did not come from a WSUS server.",
        [unchecked((int)0x8024002E)] = "Access to an unmanaged update server is not allowed.",
        [unchecked((int)0x8024002F)] = "The DisableWindowsUpdateAccess policy blocked the operation.",
        [unchecked((int)0x80240033)] = "The license terms could not be downloaded.",
        [unchecked((int)0x80240041)] = "Windows Update is not available while sysprep is running.",
        [unchecked((int)0x80240044)] = "Only administrators can perform this operation on per-computer updates.",
        [unchecked((int)0x8024004A)] = "Windows Update is not available while operating system setup is running.",
        [unchecked((int)0x80070005)] = "Access is denied. Run OnlyWinget as administrator to download or install updates.",
        [unchecked((int)0x80070422)] = "The Windows Update service is disabled or not running.",
    };

    /// <summary>
    /// HRESULTs that mean "the Windows Update service was busy or restarting": a short wait normally fixes them.
    /// </summary>
    public static bool IsTransient(int hResult) => hResult is
        unchecked((int)0x8024001E) or // WU_E_SERVICE_STOP
        unchecked((int)0x80240016) or // WU_E_INSTALL_NOT_ALLOWED
        unchecked((int)0x80240021) or // WU_E_TIME_OUT
        unchecked((int)0x8024001B) or // WU_E_SELFUPDATE_IN_PROGRESS
        unchecked((int)0x80240009); // WU_E_OPERATIONINPROGRESS

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
