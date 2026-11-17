using OnlyWinget.Application.Winget;

namespace OnlyWinget.Application.WindowsUpdate;

/// <summary>
/// Windows Update operations beyond scan/install that Windows Settings also offers: history, hide/show and uninstall.
/// Kept separate from <see cref="IWindowsUpdateService"/> so existing implementations stay valid.
/// </summary>
public interface IWindowsUpdateManagementService
{
    /// <summary>Newest-first update history (IUpdateSearcher.QueryHistory).</summary>
    Task<WindowsUpdateOperationOutcome<WindowsUpdateHistoryEntry>> GetHistoryAsync(
        int maxEntries,
        CancellationToken cancellationToken);

    /// <summary>Hides or unhides the given updates (IUpdate.IsHidden). Hidden updates are skipped by every scan.</summary>
    Task<WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>> SetHiddenAsync(
        IReadOnlyList<WindowsUpdateIdentity> updates,
        bool hidden,
        WindowsUpdateOptions options,
        CancellationToken cancellationToken);

    /// <summary>Uninstalls installed updates that report IsUninstallable (IUpdateInstaller.Uninstall).</summary>
    Task<WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>> UninstallAsync(
        IReadOnlyList<WindowsUpdateIdentity> updates,
        CancellationToken cancellationToken,
        IProgress<OperationProgress>? progress = null);
}
