namespace OnlyWinget.Application.WindowsUpdate;

public interface IWindowsUpdateService
{
    Task<WindowsUpdateOperationOutcome<WindowsUpdateItem>> ScanAsync(CancellationToken cancellationToken);

    Task<WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>> InstallAsync(
        IReadOnlyList<WindowsUpdateIdentity> updates,
        CancellationToken cancellationToken);
}
