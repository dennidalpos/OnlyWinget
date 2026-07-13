namespace OnlyWinget.Application.System;

public sealed record SystemCapabilities(
    bool? IsSupportedOs,
    bool? IsWingetAvailable,
    bool? IsPowerShellAvailable,
    bool? IsWindowsUpdateComAvailable,
    string? WindowsUpdateUnavailableReason,
    string? WingetVersion = null,
    int? WindowsBuildNumber = null)
{
    public static SystemCapabilities Unknown { get; } = new(null, null, null, null, null, null, null);

    public bool CanUseWinget => IsSupportedOs == true && IsWingetAvailable == true;

    public bool CanUseWindowsUpdate =>
        IsSupportedOs == true &&
        IsPowerShellAvailable == true &&
        IsWindowsUpdateComAvailable == true;

    public string WingetUnavailableMessage =>
        IsSupportedOs is null || IsWingetAvailable is null
            ? "System capabilities have not been checked yet."
            : IsSupportedOs == false
            ? "OnlyWinget requires Windows 10 version 1809 build 17763 or newer."
            : "winget is not available on PATH.";

    public string WindowsUpdateUnavailableMessage
    {
        get
        {
            if (IsSupportedOs == false)
            {
                return "Windows Update requires Windows 10 version 1809 build 17763 or newer.";
            }

            if (IsSupportedOs is null || IsPowerShellAvailable is null || IsWindowsUpdateComAvailable is null)
            {
                return "System capabilities have not been checked yet.";
            }

            if (IsPowerShellAvailable == false)
            {
                return "PowerShell is not available, so Windows Update cannot be inspected.";
            }

            if (IsWindowsUpdateComAvailable == false)
            {
                return string.IsNullOrWhiteSpace(WindowsUpdateUnavailableReason)
                    ? "Windows Update APIs are not available on this machine."
                    : WindowsUpdateUnavailableReason;
            }

            return "Windows Update capability has not been checked yet.";
        }
    }
}
