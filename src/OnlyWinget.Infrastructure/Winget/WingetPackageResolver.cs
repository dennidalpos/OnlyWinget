using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Infrastructure.Winget;

public sealed class WingetPackageResolver(
    IWingetCommandRunner commandRunner,
    WingetErrorClassifier errorClassifier) : IPackageResolver
{
    public async Task<PackageResolution> ResolveAsync(
        PackageIdentity package,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(package);

        var arguments = new List<string>
        {
            "show",
            "--id",
            package.Id,
            "--exact",
            "--accept-source-agreements"
        };

        if (!string.IsNullOrWhiteSpace(package.Source))
        {
            arguments.Add("--source");
            arguments.Add(package.Source);
        }

        var result = await commandRunner.RunAsync("winget", arguments, cancellationToken)
            .ConfigureAwait(false);

        var values = ParseShowOutput(result.StandardOutput);
        var resolvedPackage = new PackageIdentity(package.Id, GetValue(values, "Source", "Origine") ?? package.Source);
        return new PackageResolution(
            resolvedPackage,
            GetValue(values, "Name", "Nome"),
            GetValue(values, "Version", "Versione"),
            GetValue(values, "Publisher", "Autore", "Editore"),
            result.Succeeded,
            errorClassifier.Classify(result),
            GetValues(values, "Architecture", "Architettura"));
    }

    private static Dictionary<string, List<string>> ParseShowOutput(string output)
    {
        var values = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            {
                if (!values.TryGetValue(key, out var entries))
                {
                    entries = [];
                    values[key] = entries;
                }

                entries.Add(value);
            }
        }

        return values;
    }

    private static string? GetValue(IReadOnlyDictionary<string, List<string>> values, params string[] keys) =>
        keys.SelectMany(key => values.TryGetValue(key, out var entries) ? entries : [])
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static IReadOnlyList<string> GetValues(
        IReadOnlyDictionary<string, List<string>> values,
        params string[] keys) =>
        keys.SelectMany(key => values.TryGetValue(key, out var entries) ? entries : [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
