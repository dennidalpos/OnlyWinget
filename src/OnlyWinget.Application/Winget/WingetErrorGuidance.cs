using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Application.Winget;

/// <summary>Plain next steps for failures the user can resolve; null when there is nothing useful to add.</summary>
public static class WingetErrorGuidance
{
    public static string? For(WingetErrorKind kind, PackageAction action) => kind switch
    {
        WingetErrorKind.InstallTechnologyMismatch =>
            "The new version uses a different installer type. Uninstall the current version (Packages > Uninstall), then install the latest one.",
        WingetErrorKind.NotFound when action == PackageAction.Upgrade =>
            "winget lists this update but cannot upgrade it by id. Update it from the app itself or reinstall it with winget.",
        WingetErrorKind.CannotUpgrade =>
            "winget cannot upgrade this package. Update it from the app itself or download the installer from the vendor.",
        WingetErrorKind.InUse =>
            "Close the application (and any installer window) that uses this package, then try again.",
        WingetErrorKind.InstallInProgress =>
            "Another installation is running. Wait for it to finish, then try again.",
        WingetErrorKind.RebootRequiredFirst =>
            "Restart Windows, then try again.",
        WingetErrorKind.Pinned =>
            "The package is pinned. Remove the pin with 'winget pin remove' or enable pinned packages in Settings.",
        WingetErrorKind.PolicyBlocked =>
            "A policy on this PC blocks the operation. Contact your administrator.",
        WingetErrorKind.DiskFull =>
            "Free some disk space, then try again.",
        WingetErrorKind.Network =>
            "Check the internet connection, then try again.",
        WingetErrorKind.NoApplicableInstaller =>
            "No installer of this package fits this PC (architecture, scope or language).",
        WingetErrorKind.HashMismatch =>
            "The download did not match its checksum. Try again later or install from the vendor site.",
        WingetErrorKind.StoreUnavailable =>
            "The Microsoft Store package is not available for this account or region.",
        _ => null
    };
}
