using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Application.Winget;

/// <summary>Plain next steps for failures the user can resolve; null when there is nothing useful to add.</summary>
public static class WingetErrorGuidance
{
    public static string? For(WingetErrorKind kind, PackageAction action, Func<string, string>? localize = null) => kind switch
    {
        WingetErrorKind.InstallTechnologyMismatch => Localize("WingetGuidance_InstallTechnologyMismatch",
            "The new version uses a different installer type. Select it in WinGet Updates and choose Uninstall and reinstall.", localize),
        WingetErrorKind.NotFound when action == PackageAction.Upgrade => Localize("WingetGuidance_NotFound",
            "winget lists this update but cannot upgrade it by id. Update it from the app itself or reinstall it with winget.", localize),
        WingetErrorKind.CannotUpgrade => Localize("WingetGuidance_CannotUpgrade",
            "winget cannot upgrade this package. Update it from the app itself or download the installer from the vendor.", localize),
        WingetErrorKind.InUse => Localize("WingetGuidance_InUse",
            "Close the application (and any installer window) that uses this package, then try again.", localize),
        WingetErrorKind.InstallInProgress => Localize("WingetGuidance_InstallInProgress",
            "Another installation is running. Wait for it to finish, then try again.", localize),
        WingetErrorKind.RebootRequiredFirst => Localize("WingetGuidance_RebootRequiredFirst",
            "Restart Windows, then try again.", localize),
        WingetErrorKind.Pinned => Localize("WingetGuidance_Pinned",
            "The package is pinned. Remove the pin with 'winget pin remove' or enable pinned packages in Settings.", localize),
        WingetErrorKind.PolicyBlocked => Localize("WingetGuidance_PolicyBlocked",
            "A policy on this PC blocks the operation. Contact your administrator.", localize),
        WingetErrorKind.DiskFull => Localize("WingetGuidance_DiskFull",
            "Free some disk space, then try again.", localize),
        WingetErrorKind.Network => Localize("WingetGuidance_Network",
            "Check the internet connection, then try again.", localize),
        WingetErrorKind.NoApplicableInstaller => Localize("WingetGuidance_NoApplicableInstaller",
            "No installer of this package fits this PC (architecture, scope or language).", localize),
        WingetErrorKind.HashMismatch => Localize("WingetGuidance_HashMismatch",
            "The download did not match its checksum. Try again later or install from the vendor site.", localize),
        WingetErrorKind.StoreUnavailable => Localize("WingetGuidance_StoreUnavailable",
            "The Microsoft Store package is not available for this account or region.", localize),
        _ => null
    };

    private static string Localize(string key, string fallback, Func<string, string>? localize) =>
        localize?.Invoke(key) ?? fallback;
}
