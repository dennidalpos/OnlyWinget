using Microsoft.Extensions.Logging;
using OnlyWinget.Application.System;

namespace OnlyWinget.Infrastructure.Winget;

/// <summary>
/// winget downloads installers to <c>%TEMP%\WinGet\&lt;PackageId&gt;.&lt;version&gt;</c> and removes them itself on success.
/// A failed install or a killed process leaves that folder behind. This cleaner deletes only such folders that
/// (a) start with the failed package id and (b) were written during the failed run. Locked files are skipped.
/// </summary>
public sealed class WingetTempResidueCleaner(
    string? rootDirectory = null,
    ILogger<WingetTempResidueCleaner>? logger = null) : IInstallResidueCleaner
{
    private readonly string root = rootDirectory ?? Path.Combine(Path.GetTempPath(), "WinGet");

    public Task<IReadOnlyList<string>> CleanAsync(
        IReadOnlyCollection<string> packageIds,
        DateTimeOffset since,
        CancellationToken cancellationToken) =>
        Task.Run<IReadOnlyList<string>>(() => Clean(packageIds, since, cancellationToken), cancellationToken);

    private List<string> Clean(IReadOnlyCollection<string> packageIds, DateTimeOffset since, CancellationToken cancellationToken)
    {
        var removed = new List<string>();
        if (packageIds.Count == 0 || !Directory.Exists(root))
        {
            return removed;
        }

        // Folders are named "<PackageId>.<version>"; require the id followed by '.' so "Foo.Bar" never matches "Foo.BarBaz".
        var prefixes = packageIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim() + ".")
            .ToArray();
        var cutoff = since.UtcDateTime - TimeSpan.FromSeconds(5);

        IEnumerable<string> candidates;
        try
        {
            candidates = Directory.EnumerateDirectories(root).ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger?.LogDebug(exception, "Could not enumerate winget temp folder {Root}.", root);
            return removed;
        }

        foreach (var directory in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileName(directory);
            if (!prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            try
            {
                if (Directory.GetLastWriteTimeUtc(directory) < cutoff)
                {
                    continue;
                }

                Directory.Delete(directory, recursive: true);
                removed.Add(name);
                logger?.LogInformation("Removed leftover winget download folder {Folder}.", name);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Still in use (an installer is running) or protected: leave it; never force.
                logger?.LogDebug(exception, "Left winget download folder {Folder} in place.", name);
            }
        }

        return removed;
    }
}
