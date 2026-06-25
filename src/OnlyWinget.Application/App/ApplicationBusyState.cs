namespace OnlyWinget.Application.App;

public enum ApplicationBusyState
{
    Idle,
    LoadingWorkspace,
    SavingWorkspace,
    Searching,
    RefreshingUpdates,
    ExecutingOperation
}
