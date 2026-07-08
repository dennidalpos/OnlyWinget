namespace OnlyWinget.Presentation;

public interface IPendingNavigationGuard
{
    Task<bool> ConfirmNavigationAsync();
}
