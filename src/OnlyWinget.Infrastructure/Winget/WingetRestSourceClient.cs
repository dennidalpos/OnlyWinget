using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OnlyWinget.Application.Winget;

namespace OnlyWinget.Infrastructure.Winget;

public sealed class WingetRestSourceClient : IWingetRestSourceClient
{
    private readonly HttpClient httpClient;
    private readonly ILogger<WingetRestSourceClient>? logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public WingetRestSourceClient(HttpClient? httpClient = null, ILogger<WingetRestSourceClient>? logger = null)
    {
        this.httpClient = httpClient ?? new HttpClient();
        this.logger = logger;
    }

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "REST DTO types are known statically.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "REST DTO types are known statically.")]
    public async Task<WingetRestPackageManifest?> GetPackageManifestAsync(string sourceUrl, string packageId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);

        try
        {
            var requestUrl = $"{sourceUrl.TrimEnd('/')}/manifests/{Uri.EscapeDataString(packageId)}";
            using var response = await httpClient.GetAsync(requestUrl, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                logger?.LogWarning("REST Source API returned status code {StatusCode} for package {PackageId}", response.StatusCode, packageId);
                return null;
            }

            var dto = await response.Content.ReadFromJsonAsync<ManifestResponseDto>(JsonOptions, cancellationToken).ConfigureAwait(false);
            if (dto?.Data is null)
            {
                return null;
            }

            return MapDtoToManifest(dto.Data);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger?.LogError(exception, "Failed to retrieve package manifest for '{PackageId}' from REST source '{SourceUrl}'", packageId, sourceUrl);
            return null;
        }
    }

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "REST DTO types are known statically.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "REST DTO types are known statically.")]
    public async Task<IReadOnlyList<WingetRestPackageManifest>> SearchPackagesAsync(string sourceUrl, string query, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUrl);

        try
        {
            var requestUrl = $"{sourceUrl.TrimEnd('/')}/manifestSearch";
            var requestPayload = new SearchRequestDto(query);

            using var response = await httpClient.PostAsJsonAsync(requestUrl, requestPayload, JsonOptions, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                logger?.LogWarning("REST Source Search API returned status code {StatusCode} for query '{Query}'", response.StatusCode, query);
                return Array.Empty<WingetRestPackageManifest>();
            }

            var searchResult = await response.Content.ReadFromJsonAsync<SearchResponseDto>(JsonOptions, cancellationToken).ConfigureAwait(false);
            if (searchResult?.Data is null || searchResult.Data.Count == 0)
            {
                return Array.Empty<WingetRestPackageManifest>();
            }

            return searchResult.Data.Select(MapDtoToManifest).ToList();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger?.LogError(exception, "Failed to search packages for query '{Query}' from REST source '{SourceUrl}'", query, sourceUrl);
            return Array.Empty<WingetRestPackageManifest>();
        }
    }

    private static WingetRestPackageManifest MapDtoToManifest(ManifestDataDto dto)
    {
        return new WingetRestPackageManifest(
            PackageIdentifier: dto.PackageIdentifier ?? string.Empty,
            PackageName: dto.PackageName ?? dto.PackageIdentifier ?? string.Empty,
            Publisher: dto.Publisher ?? string.Empty,
            Author: dto.Author ?? string.Empty,
            License: dto.License ?? string.Empty,
            ShortDescription: dto.ShortDescription ?? string.Empty,
            PackageVersions: dto.Versions ?? []
        );
    }

    private sealed record ManifestResponseDto(
        [property: JsonPropertyName("Data")] ManifestDataDto? Data
    );

    private sealed record SearchResponseDto(
        [property: JsonPropertyName("Data")] List<ManifestDataDto>? Data
    );

    private sealed record SearchRequestDto(
        [property: JsonPropertyName("Query")] string Query
    );

    private sealed record ManifestDataDto(
        [property: JsonPropertyName("PackageIdentifier")] string? PackageIdentifier,
        [property: JsonPropertyName("PackageName")] string? PackageName,
        [property: JsonPropertyName("Publisher")] string? Publisher,
        [property: JsonPropertyName("Author")] string? Author,
        [property: JsonPropertyName("License")] string? License,
        [property: JsonPropertyName("ShortDescription")] string? ShortDescription,
        [property: JsonPropertyName("Versions")] List<string>? Versions
    );
}
