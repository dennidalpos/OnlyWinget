namespace OnlyWinget.Application.System;

/// <summary>
/// Removes leftovers of a failed or cancelled winget install/upgrade. Implementations must only touch artefacts that
/// belong to the named packages and were created by the failed run; they never uninstall or purge anything.
/// </summary>
public interface IInstallResidueCleaner
{
    /// <summary>
    /// Deletes winget download folders for <paramref name="packageIds"/> created at or after <paramref name="since"/>.
    /// Returns a short human-readable description of what was removed (empty when nothing was).
    /// </summary>
    Task<IReadOnlyList<string>> CleanAsync(
        IReadOnlyCollection<string> packageIds,
        DateTimeOffset since,
        CancellationToken cancellationToken);
}
