namespace OnlyWinget.Application.App;

public enum ApplicationBusyState
{
    Idle,
    LoadingWorkspace,
    CheckingWinget,
    SavingWorkspace,
    Searching,
    RefreshingUpdates,
    ManagingSources,
    ExecutingOperation
}
