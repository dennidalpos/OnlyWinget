using System.Text.RegularExpressions;

namespace OnlyWinget.Application.WindowsUpdate;

/// <summary>The same five groups Windows Settings uses for the update history.</summary>
public enum WindowsUpdateClassification
{
    Other,
    Feature,
    Quality,
    Driver,
    Definition
}

/// <summary>
/// Maps Windows Update metadata to <see cref="WindowsUpdateClassification"/>.
/// Scanned updates carry stable classification GUIDs (category type UpdateClassification). History entries do not:
/// on a real machine their categories are Product-type only with empty IDs, so history falls back to title patterns.
/// </summary>
public static partial class WindowsUpdateClassifier
{
    // Classification IDs. Drivers (ebfc1fc5-...) was confirmed on a live scan; the others are the documented WSUS
    // update classification IDs.
    private static readonly IReadOnlyDictionary<string, WindowsUpdateClassification> ByCategoryId =
        new Dictionary<string, WindowsUpdateClassification>(StringComparer.OrdinalIgnoreCase)
        {
            ["ebfc1fc5-71a4-4f7b-9aca-3b9a503104a0"] = WindowsUpdateClassification.Driver, // Drivers
            ["e0789628-ce08-4437-be74-2495b842f43b"] = WindowsUpdateClassification.Definition, // Definition Updates
            ["0fa1201d-4330-4fa8-8ae9-b877473b6441"] = WindowsUpdateClassification.Quality, // Security Updates
            ["e6cf1350-c01b-414d-a61f-263d14d133b4"] = WindowsUpdateClassification.Quality, // Critical Updates
            ["28bc880e-0592-4cbf-8f95-c79b17911d5f"] = WindowsUpdateClassification.Quality, // Update Rollups
            ["cd5ffd1e-e932-4e3a-bf74-18bf0b1bbd83"] = WindowsUpdateClassification.Quality, // Updates
            ["68c5b0a3-d1a6-4553-ae49-01d3a7827828"] = WindowsUpdateClassification.Quality, // Service Packs
            ["3689bdc8-b205-4af4-8d4a-a63924c5e9d5"] = WindowsUpdateClassification.Feature, // Upgrades
            ["b54e7d24-7add-428f-8b75-90a396fa584f"] = WindowsUpdateClassification.Feature // Feature Packs
        };

    public static WindowsUpdateClassification FromCategoryIds(IEnumerable<string> categoryIds, string? title = null)
    {
        foreach (var id in categoryIds)
        {
            if (!string.IsNullOrWhiteSpace(id) && ByCategoryId.TryGetValue(id.Trim(), out var classification))
            {
                return classification;
            }
        }

        return FromTitle(title);
    }

    /// <summary>Best-effort classification from an update title (English and Italian wording).</summary>
    public static WindowsUpdateClassification FromTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return WindowsUpdateClassification.Other;
        }

        if (DefinitionPattern().IsMatch(title))
        {
            return WindowsUpdateClassification.Definition;
        }

        if (FeaturePattern().IsMatch(title))
        {
            return WindowsUpdateClassification.Feature;
        }

        if (QualityPattern().IsMatch(title))
        {
            return WindowsUpdateClassification.Quality;
        }

        if (DriverPattern().IsMatch(title))
        {
            return WindowsUpdateClassification.Driver;
        }

        return WindowsUpdateClassification.Other;
    }

    [GeneratedRegex(@"definition update|aggiornamento delle definizioni|security intelligence|intelligence sulla sicurezza|aggiornamento dell'intelligence|definizioni? di", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DefinitionPattern();

    [GeneratedRegex(@"feature update|aggiornamento delle funzionalit|upgrade to windows|windows 1[01], version \w+ *$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FeaturePattern();

    [GeneratedRegex(@"cumulative update|aggiornamento cumulativo|security update|aggiornamento (della|di) sicurezza|servicing stack|aggiornamento dello stack di manutenzione|\.NET Framework|\(KB\d{6,8}\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QualityPattern();

    // Driver titles are "<Vendor> - <Class> - <version>" (for example "Intel - System - 10.1.54.3") or say "driver".
    [GeneratedRegex(@"\bdriver\b|^.+ - .+ - \d+(\.\d+){1,3}\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DriverPattern();
}
