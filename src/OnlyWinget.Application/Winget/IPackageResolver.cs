using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Application.Winget;

public interface IPackageResolver
{
    Task<PackageResolution> ResolveAsync(PackageIdentity package, CancellationToken cancellationToken);
    Task<PackageInstalledStatus> CheckInstalledStatusAsync(PackageIdentity package, CancellationToken cancellationToken);
}
