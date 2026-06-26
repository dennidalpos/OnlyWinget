namespace OnlyWinget.Application.App;

public enum ApplicationBusyState
{
    Idle,
    LoadingWorkspace,
    CheckingWinget,
    SavingWorkspace,
    Searching,
    RefreshingUpdates,
    ScanningWindowsUpdates,
    InstallingWindowsUpdates,
    ManagingSources,
    ExecutingOperation
}
