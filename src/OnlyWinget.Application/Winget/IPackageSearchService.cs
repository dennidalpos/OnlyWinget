namespace OnlyWinget.Application.Winget;

public interface IPackageSearchService
{
    Task<IReadOnlyList<PackageSearchResult>> SearchAsync(
        PackageSearchRequest request,
        CancellationToken cancellationToken);
}
