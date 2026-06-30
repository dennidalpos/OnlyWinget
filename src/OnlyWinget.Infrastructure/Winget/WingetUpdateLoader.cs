using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Infrastructure.Winget;

public sealed class WingetUpdateLoader(
    IWingetCommandRunner commandRunner,
    WingetTableParser tableParser,
    WingetErrorClassifier errorClassifier) : IUpdateLoader
{
    public async Task<WingetOperationOutcome<PackageUpdate>> LoadUpdatesAsync(string source, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        var result = await commandRunner.RunAsync(
                "winget",
                ["upgrade", "--source", source, "--accept-source-agreements", "--disable-interactivity"],
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
            .Select(row => ToUpdate(row, source))
            .Where(update => update is not null)
            .Cast<PackageUpdate>()
            .ToArray();
        return WingetOperationOutcome<PackageUpdate>.Success(rows, rawOutput);
    }

    private static PackageUpdate? ToUpdate(IReadOnlyDictionary<string, string> row, string requestedSource)
    {
        if (!TryGetAny(row, out var id, "Id") ||
            !TryGetAny(row, out var version, "Version", "Versione") ||
            !TryGetAny(row, out var available, "Available", "Disponibile") ||
            id.Any(char.IsWhiteSpace))
        {
            return null;
        }

        TryGetAny(row, out var name, "Name", "Nome");
        TryGetAny(row, out var source, "Source", "Origine");

        return new PackageUpdate(
            new PackageIdentity(id, string.IsNullOrWhiteSpace(source) ? requestedSource : source),
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
