namespace OnlyWinget.Application.Winget;

public interface ICommandAvailability
{
    Task<bool> IsWingetAvailableAsync(CancellationToken cancellationToken);
}
