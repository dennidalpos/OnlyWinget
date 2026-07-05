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

        var rawOutput = WingetOutputHelpers.JoinOutput(result);
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
        if (!WingetOutputHelpers.TryGet(row, "Id", out var id) ||
            !WingetOutputHelpers.TryGet(row, "Version", out var version) ||
            !WingetOutputHelpers.TryGet(row, "Available", out var available) ||
            id.Any(char.IsWhiteSpace))
        {
            return null;
        }

        WingetOutputHelpers.TryGet(row, "Name", out var name);
        WingetOutputHelpers.TryGet(row, "Source", out var source);

        return new PackageUpdate(
            new PackageIdentity(id, string.IsNullOrWhiteSpace(source) ? requestedSource : source),
            string.IsNullOrWhiteSpace(name) ? id : name,
            version,
            available);
    }
}
