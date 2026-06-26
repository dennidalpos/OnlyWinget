namespace OnlyWinget.Application.App;

public enum ApplicationBusyState
{
    Idle,
    LoadingWorkspace,
    CheckingCapabilities,
    SavingWorkspace,
    Searching,
    RefreshingUpdates,
    ScanningWindowsUpdates,
    InstallingWindowsUpdates,
    ManagingSources,
    ExecutingOperation
}
