using System.Runtime.Versioning;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Infrastructure.Winget;

[SupportedOSPlatform("windows")]
public sealed class ComWingetPackageService(
    WingetPackageSearchService fallbackSearchService,
    WingetPackageResolver fallbackResolverService,
    IMemoryCache? cache = null,
    ILogger<ComWingetPackageService>? logger = null) : IPackageSearchService, IPackageResolver
{
    private static readonly Guid WinGetPackageManagerClsid = new("c84f4a4d-068d-4e92-beab-73e449ff39a8");
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<WingetOperationOutcome<PackageSearchResult>> SearchAsync(
        PackageSearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var cacheKey = $"com_winget_search_{request.Query}_{request.Source}";
        if (cache is not null && cache.TryGetValue(cacheKey, out WingetOperationOutcome<PackageSearchResult>? cachedOutcome) && cachedOutcome is not null)
        {
            return cachedOutcome;
        }

        if (OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(request.Query))
        {
            try
            {
                var packageManagerType = Type.GetTypeFromCLSID(WinGetPackageManagerClsid);
                if (packageManagerType is not null)
                {
                    var outcome = await Task.Run(() => SearchNativeCom(request), cancellationToken).ConfigureAwait(false);
                    if (outcome is not null && outcome.Succeeded)
                    {
                        logger?.LogInformation("WinGet native COM search completed for query '{Query}' with {Count} results.", request.Query, outcome.Rows.Count);
                        cache?.Set(cacheKey, outcome, CacheDuration);
                        return outcome;
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "WinGet native COM search failed for query '{Query}'. Falling back to CLI search.", request.Query);
            }
        }

        return await fallbackSearchService.SearchAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PackageResolution> ResolveAsync(PackageIdentity package, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(package);

        if (OperatingSystem.IsWindows())
        {
            try
            {
                var packageManagerType = Type.GetTypeFromCLSID(WinGetPackageManagerClsid);
                if (packageManagerType is not null)
                {
                    var outcome = await Task.Run(() => ResolveNativeCom(package), cancellationToken).ConfigureAwait(false);
                    if (outcome is not null && outcome.IsResolved)
                    {
                        logger?.LogInformation("WinGet native COM package resolution succeeded for package '{Id}'.", package.Id);
                        return outcome;
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "WinGet native COM resolution failed for '{Id}'. Falling back to CLI resolution.", package.Id);
            }
        }

        return await fallbackResolverService.ResolveAsync(package, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PackageInstalledStatus> CheckInstalledStatusAsync(PackageIdentity package, CancellationToken cancellationToken)
    {
        return await fallbackResolverService.CheckInstalledStatusAsync(package, cancellationToken).ConfigureAwait(false);
    }

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "WinGet COM dynamic invocation is protected by try-catch fallback.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "WinGet COM CLSID type instantiation.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "WinGet COM dynamic invocation is protected by try-catch fallback.")]
    private static WingetOperationOutcome<PackageSearchResult>? SearchNativeCom(PackageSearchRequest request)
    {
        var packageManagerType = Type.GetTypeFromCLSID(WinGetPackageManagerClsid);
        if (packageManagerType is null) return null;

        dynamic packageManager = Activator.CreateInstance(packageManagerType)!;
        dynamic catalogReference = packageManager.GetPredefinedPackageCatalog(0); // OpenWingetCatalog
        dynamic connectResult = catalogReference.Connect();

        if (connectResult.Status != 0) return null;

        dynamic catalog = connectResult.PackageCatalog;
        dynamic findOptions = packageManager.CreateFindPackagesOptions();
        dynamic filter = packageManager.CreatePackageMatchFilter();

        filter.Field = 0; // Id/Name
        filter.Option = 1; // Contains
        filter.Value = request.Query;
        findOptions.Filters.Add(filter);

        dynamic findResult = catalog.FindPackages(findOptions);
        dynamic matches = findResult.Matches;

        var results = new List<PackageSearchResult>();
        int count = matches.Count;

        for (int i = 0; i < count; i++)
        {
            dynamic match = matches.Item(i);
            dynamic catalogPackage = match.CatalogPackage;

            string id = catalogPackage.Id?.ToString() ?? string.Empty;
            string name = catalogPackage.Name?.ToString() ?? id;
            string version = catalogPackage.DefaultInstallVersion?.Version?.ToString() ?? "Latest";
            string source = request.Source ?? "winget";

            if (!string.IsNullOrWhiteSpace(id))
            {
                results.Add(new PackageSearchResult(
                    new PackageIdentity(id, source),
                    name,
                    version,
                    Match: null));
            }
        }

        return WingetOperationOutcome<PackageSearchResult>.Success(results, "COM Native Search Completed");
    }

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "WinGet COM dynamic invocation is protected by try-catch fallback.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "WinGet COM CLSID type instantiation.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "WinGet COM dynamic invocation is protected by try-catch fallback.")]
    private static PackageResolution? ResolveNativeCom(PackageIdentity package)
    {
        var packageManagerType = Type.GetTypeFromCLSID(WinGetPackageManagerClsid);
        if (packageManagerType is null) return null;

        dynamic packageManager = Activator.CreateInstance(packageManagerType)!;
        dynamic catalogReference = packageManager.GetPredefinedPackageCatalog(0);
        dynamic connectResult = catalogReference.Connect();

        if (connectResult.Status != 0) return null;

        dynamic catalog = connectResult.PackageCatalog;
        dynamic findOptions = packageManager.CreateFindPackagesOptions();
        dynamic filter = packageManager.CreatePackageMatchFilter();

        filter.Field = 0; // Id
        filter.Option = 0; // Exact match
        filter.Value = package.Id;
        findOptions.Filters.Add(filter);

        dynamic findResult = catalog.FindPackages(findOptions);
        if (findResult.Matches.Count == 0) return null;

        dynamic match = findResult.Matches.Item(0);
        dynamic catalogPackage = match.CatalogPackage;
        dynamic defaultVersion = catalogPackage.DefaultInstallVersion;

        string id = catalogPackage.Id?.ToString() ?? package.Id;
        string name = catalogPackage.Name?.ToString() ?? id;
        string version = defaultVersion?.Version?.ToString() ?? "Latest";
        string publisher = defaultVersion?.Publisher?.ToString() ?? "Unknown";

        return new PackageResolution(
            new PackageIdentity(id, package.Source),
            name,
            version,
            publisher,
            IsResolved: true,
            Error: null);
    }
}
