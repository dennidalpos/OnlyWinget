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
        var resolvedPackage = new PackageIdentity(package.Id, GetValue(values, "Source") ?? package.Source);
        return new PackageResolution(
            resolvedPackage,
            GetValue(values, "Name"),
            GetValue(values, "Version"),
            GetValue(values, "Publisher"),
            result.Succeeded,
            errorClassifier.Classify(result));
    }

    private static Dictionary<string, string> ParseShowOutput(string output)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
                values[key] = value;
            }
        }

        return values;
    }

    private static string? GetValue(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;
}
