namespace OnlyWinget.Application.Winget;

public record WingetRestPackageManifest(
    string PackageIdentifier,
    string PackageName,
    string Publisher,
    string Author,
    string License,
    string ShortDescription,
    IReadOnlyList<string> PackageVersions
);

public interface IWingetRestSourceClient
{
    Task<WingetRestPackageManifest?> GetPackageManifestAsync(string sourceUrl, string packageId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WingetRestPackageManifest>> SearchPackagesAsync(string sourceUrl, string query, CancellationToken cancellationToken = default);
}
