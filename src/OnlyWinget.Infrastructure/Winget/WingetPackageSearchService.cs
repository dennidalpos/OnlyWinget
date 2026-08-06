using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Infrastructure.Winget;

public sealed class WingetPackageSearchService(
    IWingetCommandRunner commandRunner,
    WingetTableParser tableParser,
    WingetErrorClassifier errorClassifier) : IPackageSearchService
{
    public async Task<WingetOperationOutcome<PackageSearchResult>> SearchAsync(
        PackageSearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var arguments = new List<string>
        {
            "search",
            request.Query,
            "--count",
            "1000",
            "--accept-source-agreements",
            "--disable-interactivity"
        };

        if (!string.IsNullOrWhiteSpace(request.Source))
        {
            arguments.Add("--source");
            arguments.Add(request.Source);
        }

        var result = await commandRunner.RunAsync("winget", arguments, cancellationToken)
            .ConfigureAwait(false);

        var rawOutput = WingetOutputHelpers.JoinOutput(result);
        if (!result.Succeeded)
        {
            return WingetOperationOutcome<PackageSearchResult>.Failure(
                errorClassifier.Classify(result) ?? new ClassifiedWingetError(WingetErrorKind.Unknown, "winget search failed."),
                rawOutput);
        }

        var rows = tableParser.Parse(result.StandardOutput)
            .Select(row => ToSearchResult(row, request.Source))
            .Where(searchResult => searchResult is not null)
            .Cast<PackageSearchResult>()
            .ToArray();
        return WingetOperationOutcome<PackageSearchResult>.Success(rows, rawOutput);
    }

    private static PackageSearchResult? ToSearchResult(
        IReadOnlyDictionary<string, string> row,
        string? requestedSource)
    {
        if (!WingetTableParser.IsValidRow(row) || !WingetOutputHelpers.TryGet(row, "Id", out var id))
        {
            return null;
        }

        WingetOutputHelpers.TryGet(row, "Name", out var name);
        WingetOutputHelpers.TryGet(row, "Version", out var version);
        WingetOutputHelpers.TryGet(row, "Match", out var match);
        WingetOutputHelpers.TryGet(row, "Source", out var source);

        return new PackageSearchResult(
            new PackageIdentity(id, string.IsNullOrWhiteSpace(source) ? requestedSource : source),
            string.IsNullOrWhiteSpace(name) ? id : name,
            EmptyToNull(version),
            EmptyToNull(match));
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
