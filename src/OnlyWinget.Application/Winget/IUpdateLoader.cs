namespace OnlyWinget.Application.Winget;

public interface IUpdateLoader
{
    Task<WingetOperationOutcome<PackageUpdate>> LoadUpdatesAsync(CancellationToken cancellationToken);
}
