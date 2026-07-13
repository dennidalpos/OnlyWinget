using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Infrastructure.Winget;

public sealed class WingetPackageResolver(
    IWingetCommandRunner commandRunner,
    WingetTableParser tableParser,
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
            "--accept-source-agreements",
            "--disable-interactivity"
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

        var name = GetValue(values, "Name", "Nome");
        if (string.IsNullOrWhiteSpace(name))
        {
            name = ExtractName(result.StandardOutput, package.Id) ?? package.Id;
        }

        return new PackageResolution(
            resolvedPackage,
            name,
            GetValue(values, "Version", "Versione"),
            GetValue(values, "Publisher", "Autore", "Editore"),
            result.Succeeded,
            errorClassifier.Classify(result),
            GetValues(values, "Architecture", "Architettura"));
    }

    public async Task<PackageInstalledStatus> CheckInstalledStatusAsync(
        PackageIdentity package,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(package);

        var arguments = new List<string>
        {
            "list",
            "--id",
            package.Id,
            "--exact",
            "--accept-source-agreements",
            "--disable-interactivity"
        };

        if (!string.IsNullOrWhiteSpace(package.Source))
        {
            arguments.Add("--source");
            arguments.Add(package.Source);
        }

        try
        {
            var result = await commandRunner.RunAsync("winget", arguments, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Succeeded)
            {
                return new PackageInstalledStatus(false, null);
            }

            var values = tableParser.Parse(result.StandardOutput);
            var matchingRow = values.FirstOrDefault(row =>
                WingetOutputHelpers.TryGet(row, "Id", out var id) &&
                string.Equals(id.Trim(), package.Id, StringComparison.OrdinalIgnoreCase));

            if (matchingRow is not null && WingetOutputHelpers.TryGet(matchingRow, "Version", out var version))
            {
                return new PackageInstalledStatus(true, version.Trim());
            }

            return new PackageInstalledStatus(false, null);
        }
        catch (Exception)
        {
            return new PackageInstalledStatus(false, null);
        }
    }

    private static string? ExtractName(string output, string packageId)
    {
        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var openBracket = line.LastIndexOf('[');
            var closeBracket = line.LastIndexOf(']');
            if (openBracket > 0 && closeBracket > openBracket)
            {
                var idInBrackets = line[(openBracket + 1)..closeBracket].Trim();
                if (string.Equals(idInBrackets, packageId, StringComparison.OrdinalIgnoreCase))
                {
                    var firstSpace = line.IndexOf(' ');
                    if (firstSpace > 0 && firstSpace < openBracket)
                    {
                        var name = line[firstSpace..openBracket].Trim();
                        if (!string.IsNullOrEmpty(name))
                        {
                            return name;
                        }
                    }
                }
            }
        }
        return null;
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
