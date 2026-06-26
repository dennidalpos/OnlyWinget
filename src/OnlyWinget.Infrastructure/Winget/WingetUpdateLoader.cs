using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Infrastructure.Winget;

public sealed class WingetUpdateLoader(
    IWingetCommandRunner commandRunner,
    WingetTableParser tableParser,
    WingetErrorClassifier errorClassifier) : IUpdateLoader
{
    public async Task<WingetOperationOutcome<PackageUpdate>> LoadUpdatesAsync(CancellationToken cancellationToken)
    {
        var result = await commandRunner.RunAsync(
                "winget",
                ["upgrade", "--accept-source-agreements"],
                cancellationToken)
            .ConfigureAwait(false);

        var rawOutput = JoinOutput(result);
        if (!result.Succeeded)
        {
            return WingetOperationOutcome<PackageUpdate>.Failure(
                errorClassifier.Classify(result) ?? new ClassifiedWingetError(WingetErrorKind.Unknown, "winget upgrade failed."),
                rawOutput);
        }

        var rows = tableParser.Parse(result.StandardOutput)
            .Select(ToUpdate)
            .Where(update => update is not null)
            .Cast<PackageUpdate>()
            .ToArray();
        return WingetOperationOutcome<PackageUpdate>.Success(rows, rawOutput);
    }

    private static PackageUpdate? ToUpdate(IReadOnlyDictionary<string, string> row)
    {
        if (!TryGetAny(row, out var id, "Id") ||
            !TryGetAny(row, out var version, "Version", "Versione") ||
            !TryGetAny(row, out var available, "Available", "Disponibile"))
        {
            return null;
        }

        TryGetAny(row, out var name, "Name", "Nome");
        TryGetAny(row, out var source, "Source", "Origine");

        return new PackageUpdate(
            new PackageIdentity(id, source),
            string.IsNullOrWhiteSpace(name) ? id : name,
            version,
            available);
    }

    private static bool TryGet(IReadOnlyDictionary<string, string> row, string key, out string value) =>
        row.TryGetValue(key, out value!) && !string.IsNullOrWhiteSpace(value);

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

    private static string JoinOutput(WingetCommandResult result) =>
        string.Join(Environment.NewLine, result.StandardOutput, result.StandardError).Trim();
}
