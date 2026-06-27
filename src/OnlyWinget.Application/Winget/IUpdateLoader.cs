namespace OnlyWinget.Application.Winget;

public interface IUpdateLoader
{
    Task<WingetOperationOutcome<PackageUpdate>> LoadUpdatesAsync(string source, CancellationToken cancellationToken);
}
