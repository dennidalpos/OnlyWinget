namespace OnlyWinget.Application.Winget;

/// <summary>
/// User-controlled switches that widen what winget reports and upgrades. Both are off by default because winget
/// itself hides these packages from <c>upgrade</c>.
/// </summary>
public sealed class UpdateScanOptions
{
    /// <summary>Pass <c>--include-unknown</c>: list and upgrade packages whose installed version cannot be determined.</summary>
    public volatile bool IncludeUnknownVersions;

    /// <summary>Pass <c>--include-pinned</c>: list and upgrade packages that are pinned.</summary>
    public volatile bool IncludePinned;
}
