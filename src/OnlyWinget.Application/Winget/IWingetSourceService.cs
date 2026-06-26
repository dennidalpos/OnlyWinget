namespace OnlyWinget.Application.Winget;

public interface IWingetSourceService
{
    Task<WingetOperationOutcome<WingetSource>> ListSourcesAsync(CancellationToken cancellationToken);

    Task<WingetOperationOutcome<WingetSource>> UpdateSourcesAsync(CancellationToken cancellationToken);

    Task<WingetOperationOutcome<WingetSource>> AddSourceAsync(
        string name,
        string argument,
        CancellationToken cancellationToken);

    Task<WingetOperationOutcome<WingetSource>> RemoveSourceAsync(
        string name,
        CancellationToken cancellationToken);

    Task<WingetOperationOutcome<WingetSource>> ResetSourcesAsync(CancellationToken cancellationToken);
}
