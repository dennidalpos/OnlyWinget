using System.Text.RegularExpressions;
using System.Web;

namespace OnlyWinget.Application.Navigation;

public enum UrlProtocolAction
{
    Unknown,
    Install,
    Show,
    Search
}

public sealed record UrlProtocolRequest(
    UrlProtocolAction Action,
    string? PackageId,
    string? Query,
    bool IsValid)
{
    public static UrlProtocolRequest Invalid => new(UrlProtocolAction.Unknown, null, null, false);
}

public static class UrlProtocolParser
{
    private static readonly Regex PackageIdRegex = new(@"^[a-zA-Z0-9_\-\.]{2,128}$", RegexOptions.Compiled);

    public static UrlProtocolRequest Parse(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return UrlProtocolRequest.Invalid;
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, "onlywinget", StringComparison.OrdinalIgnoreCase))
        {
            return UrlProtocolRequest.Invalid;
        }

        var hostAndPath = uri.Host + uri.AbsolutePath;
        var actionName = hostAndPath.Trim('/', ' ').ToLowerInvariant();

        var action = actionName switch
        {
            "install" => UrlProtocolAction.Install,
            "show" or "details" => UrlProtocolAction.Show,
            "search" or "find" => UrlProtocolAction.Search,
            _ => UrlProtocolAction.Unknown
        };

        if (action == UrlProtocolAction.Unknown)
        {
            return UrlProtocolRequest.Invalid;
        }

        var queryParameters = HttpUtility.ParseQueryString(uri.Query);
        var packageId = queryParameters["packageId"] ?? queryParameters["id"];
        var rawQuery = queryParameters["query"] ?? queryParameters["q"];

        var sanitizedPackageId = SanitizePackageId(packageId);
        var sanitizedQuery = SanitizeQuery(rawQuery);

        if (action is UrlProtocolAction.Install or UrlProtocolAction.Show && string.IsNullOrWhiteSpace(sanitizedPackageId))
        {
            return UrlProtocolRequest.Invalid;
        }

        if (action is UrlProtocolAction.Search && string.IsNullOrWhiteSpace(sanitizedQuery) && string.IsNullOrWhiteSpace(sanitizedPackageId))
        {
            return UrlProtocolRequest.Invalid;
        }

        return new UrlProtocolRequest(action, sanitizedPackageId, sanitizedQuery ?? sanitizedPackageId, true);
    }

    public static string? SanitizePackageId(string? rawPackageId)
    {
        if (string.IsNullOrWhiteSpace(rawPackageId))
        {
            return null;
        }

        var trimmed = rawPackageId.Trim();
        return PackageIdRegex.IsMatch(trimmed) ? trimmed : null;
    }

    public static string? SanitizeQuery(string? rawQuery)
    {
        if (string.IsNullOrWhiteSpace(rawQuery))
        {
            return null;
        }

        var sanitized = Regex.Replace(rawQuery, @"[\r\n\t\0;|&<>`""]", string.Empty).Trim();
        return sanitized.Length > 0 && sanitized.Length <= 256 ? sanitized : null;
    }
}
