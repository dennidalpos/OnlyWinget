using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Infrastructure.Winget;

public sealed class WingetPackageSearchService(
    IWingetCommandRunner commandRunner,
    WingetTableParser tableParser) : IPackageSearchService
{
    public async Task<IReadOnlyList<PackageSearchResult>> SearchAsync(
        PackageSearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var arguments = new List<string>
        {
            "search",
            request.Query,
            "--accept-source-agreements"
        };

        if (!string.IsNullOrWhiteSpace(request.Source))
        {
            arguments.Add("--source");
            arguments.Add(request.Source);
        }

        var result = await commandRunner.RunAsync("winget", arguments, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return [];
        }

        return tableParser.Parse(result.StandardOutput)
            .Select(ToSearchResult)
            .Where(searchResult => searchResult is not null)
            .Cast<PackageSearchResult>()
            .ToArray();
    }

    private static PackageSearchResult? ToSearchResult(IReadOnlyDictionary<string, string> row)
    {
        if (!TryGet(row, "Id", out var id) || string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        TryGet(row, "Name", out var name);
        TryGet(row, "Version", out var version);
        TryGet(row, "Match", out var match);
        TryGet(row, "Source", out var source);

        return new PackageSearchResult(
            new PackageIdentity(id, source),
            string.IsNullOrWhiteSpace(name) ? id : name,
            EmptyToNull(version),
            EmptyToNull(match));
    }

    private static bool TryGet(IReadOnlyDictionary<string, string> row, string key, out string value) =>
        row.TryGetValue(key, out value!);

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
