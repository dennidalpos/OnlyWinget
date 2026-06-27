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

        var rawOutput = JoinOutput(result);
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
        if (!TryGetAny(row, out var id, "Id") || string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        TryGetAny(row, out var name, "Name", "Nome");
        TryGetAny(row, out var version, "Version", "Versione");
        TryGetAny(row, out var match, "Match", "Corrispondenza");
        TryGetAny(row, out var source, "Source", "Origine");

        return new PackageSearchResult(
            new PackageIdentity(id, string.IsNullOrWhiteSpace(source) ? requestedSource : source),
            string.IsNullOrWhiteSpace(name) ? id : name,
            EmptyToNull(version),
            EmptyToNull(match));
    }

    private static bool TryGet(IReadOnlyDictionary<string, string> row, string key, out string value) =>
        row.TryGetValue(key, out value!);

    private static bool TryGetAny(IReadOnlyDictionary<string, string> row, out string value, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (TryGet(row, key, out value))
            {
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string JoinOutput(WingetCommandResult result) =>
        string.Join(Environment.NewLine, result.StandardOutput, result.StandardError).Trim();
}
