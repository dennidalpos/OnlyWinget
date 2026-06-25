using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Infrastructure.Winget;

public sealed class WingetUpdateLoader(
    IWingetCommandRunner commandRunner,
    WingetTableParser tableParser) : IUpdateLoader
{
    public async Task<IReadOnlyList<PackageUpdate>> LoadUpdatesAsync(CancellationToken cancellationToken)
    {
        var result = await commandRunner.RunAsync(
                "winget",
                ["upgrade", "--accept-source-agreements"],
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return [];
        }

        return tableParser.Parse(result.StandardOutput)
            .Select(ToUpdate)
            .Where(update => update is not null)
            .Cast<PackageUpdate>()
            .ToArray();
    }

    private static PackageUpdate? ToUpdate(IReadOnlyDictionary<string, string> row)
    {
        if (!TryGet(row, "Id", out var id) ||
            !TryGet(row, "Version", out var version) ||
            !TryGet(row, "Available", out var available))
        {
            return null;
        }

        TryGet(row, "Name", out var name);
        TryGet(row, "Source", out var source);

        return new PackageUpdate(
            new PackageIdentity(id, source),
            string.IsNullOrWhiteSpace(name) ? id : name,
            version,
            available);
    }

    private static bool TryGet(IReadOnlyDictionary<string, string> row, string key, out string value) =>
        row.TryGetValue(key, out value!) && !string.IsNullOrWhiteSpace(value);
}
