namespace OnlyWinget.Application.Winget;

public interface IPackageSearchService
{
    Task<WingetOperationOutcome<PackageSearchResult>> SearchAsync(
        PackageSearchRequest request,
        CancellationToken cancellationToken);
}
