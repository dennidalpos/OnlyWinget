// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OnlyWinget.Models;

namespace OnlyWinget.Services;

public sealed class WingetQueryService
{
    private readonly WingetCommandService _wingetService;

    public WingetQueryService(WingetCommandService wingetService)
    {
        _wingetService = wingetService ?? throw new ArgumentNullException(nameof(wingetService));
    }

    public bool TestAppExists(string id, string source = "winget")
    {
        var parameters = new Dictionary<string, string?>
        {
            ["--id"] = id,
            ["--exact"] = null,
            ["--accept-source-agreements"] = null
        };

        if (!string.IsNullOrWhiteSpace(source))
        {
            parameters["--source"] = source;
        }

        var result = _wingetService.Invoke("show", parameters);
        return result.ExitCode == 0;
    }

    public SavedPackageResolutionResult ResolveSavedPackage(
        string id,
        string name,
        string? source = "winget",
        CancellationToken cancellationToken = default)
    {
        return ResolveSavedPackageAsync(id, name, source, cancellationToken).GetAwaiter().GetResult();
    }

    public async Task<SavedPackageResolutionResult> ResolveSavedPackageAsync(
        string id,
        string name,
        string? source = "winget",
        CancellationToken cancellationToken = default)
    {
        var normalizedId = (id ?? string.Empty).Trim();
        var normalizedName = (name ?? string.Empty).Trim();
        var normalizedSource = AppEntry.NormalizeSource(source);
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return SavedPackageResolutionResult.Unresolved(normalizedId, normalizedName, normalizedSource);
        }

        var exactIdResult = await _wingetService
            .InvokeAsync("show", CreatePackageLookupParameters("--id", normalizedId, normalizedSource, exact: true), null, cancellationToken)
            .ConfigureAwait(false);
        if (exactIdResult.ExitCode == 0 && !IsAmbiguousPackageOutput(exactIdResult.Output))
        {
            return SavedPackageResolutionResult.Resolved(normalizedId, normalizedName, normalizedSource);
        }

        if (IsAmbiguousPackageOutput(exactIdResult.Output))
        {
            return SavedPackageResolutionResult.Ambiguous(normalizedId, normalizedName, normalizedSource);
        }

        var idSearchResolution = ResolveUniqueSearchCandidate(
            await SearchPackagesAsync("--id", normalizedId, normalizedSource, cancellationToken).ConfigureAwait(false),
            normalizedId,
            normalizedName,
            normalizedSource,
            candidate => string.Equals(candidate.Id, normalizedId, StringComparison.OrdinalIgnoreCase));
        if (idSearchResolution.Status != SavedPackageResolutionStatus.Unresolved)
        {
            return idSearchResolution;
        }

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return SavedPackageResolutionResult.Unresolved(normalizedId, normalizedName, normalizedSource);
        }

        return ResolveUniqueSearchCandidate(
            await SearchPackagesAsync("--name", normalizedName, normalizedSource, cancellationToken).ConfigureAwait(false),
            normalizedId,
            normalizedName,
            normalizedSource,
            candidate => string.Equals(candidate.Name, normalizedName, StringComparison.CurrentCultureIgnoreCase));
    }

    public IReadOnlyList<SearchResult> Search(string query, CancellationToken cancellationToken = default)
    {
        return SearchAsync(query, cancellationToken).GetAwaiter().GetResult();
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var result = await _wingetService
            .InvokeAsync("search", new Dictionary<string, string?>
            {
                ["--query"] = query,
                ["--accept-source-agreements"] = null
            }, null, cancellationToken)
            .ConfigureAwait(false);

        var parsedResults = WingetTableParser.ParseSearchResults(result.Output);
        if (!parsedResults.Any(NeedsSearchResultExpansion))
        {
            return parsedResults;
        }

        var results = new List<SearchResult>(parsedResults.Count);
        foreach (var parsedResult in parsedResults)
        {
            results.Add(await ExpandSearchResultAsync(parsedResult, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    public IReadOnlyList<UpdateEntry> LoadUpdates(CancellationToken cancellationToken = default)
    {
        return LoadUpdatesAsync(cancellationToken).GetAwaiter().GetResult();
    }

    public async Task<IReadOnlyList<UpdateEntry>> LoadUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var updatesResult = await _wingetService
            .InvokeAsync("list", new Dictionary<string, string?>
            {
                ["--upgrade-available"] = null,
                ["--include-unknown"] = null,
                ["--include-pinned"] = null,
                ["--accept-source-agreements"] = null
            }, null, cancellationToken)
            .ConfigureAwait(false);

        var updates = WingetTableParser.ParseUpgradeEntries(updatesResult.Output);
        return updates
            .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public UpdateEntry? FindAvailableUpdate(string id, string? source = "winget", CancellationToken cancellationToken = default)
    {
        return FindAvailableUpdateAsync(id, source, cancellationToken).GetAwaiter().GetResult();
    }

    public async Task<UpdateEntry?> FindAvailableUpdateAsync(string id, string? source = "winget", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var matchingUpdates = (await LoadUpdatesAsync(cancellationToken).ConfigureAwait(false))
            .Where(update => string.Equals(update.Id, id, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matchingUpdates.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            var sourceMatch = matchingUpdates.FirstOrDefault(update => string.Equals(update.Source, source, StringComparison.OrdinalIgnoreCase));
            if (sourceMatch != null)
            {
                return sourceMatch;
            }
        }

        return matchingUpdates[0];
    }

    public WingetPackageDetails TryLoadInstalledPackageDetails(string id, string? source = "winget", CancellationToken cancellationToken = default)
    {
        return TryLoadInstalledPackageDetailsAsync(id, source, cancellationToken).GetAwaiter().GetResult();
    }

    public async Task<WingetPackageDetails> TryLoadInstalledPackageDetailsAsync(string id, string? source = "winget", CancellationToken cancellationToken = default)
    {
        try
        {
            return await LoadInstalledPackageDetailsAsync(id, source, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new WingetPackageDetails();
        }
    }

    private async Task<IReadOnlyList<SearchResult>> SearchPackagesAsync(
        string option,
        string value,
        string source,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<SearchResult>();
        }

        var result = await _wingetService
            .InvokeAsync("search", CreatePackageLookupParameters(option, value, source, exact: false), null, cancellationToken)
            .ConfigureAwait(false);
        if (result.ExitCode != 0 || IsAmbiguousPackageOutput(result.Output))
        {
            return Array.Empty<SearchResult>();
        }

        return WingetTableParser.ParseSearchResults(result.Output)
            .Where(candidate => string.IsNullOrWhiteSpace(source) || string.Equals(candidate.Source, source, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private async Task<SearchResult> ExpandSearchResultAsync(SearchResult result, CancellationToken cancellationToken)
    {
        if (!NeedsSearchResultExpansion(result))
        {
            return result;
        }

        var idQuery = GetSearchLookupPrefix(result.Id);
        if (string.IsNullOrWhiteSpace(idQuery))
        {
            return result;
        }

        var parameters = new Dictionary<string, string?>
        {
            ["--id"] = idQuery,
            ["--accept-source-agreements"] = null,
            ["--disable-interactivity"] = null
        };

        if (!string.IsNullOrWhiteSpace(result.Source))
        {
            parameters["--source"] = result.Source;
        }

        var expandedResult = await _wingetService.InvokeAsync("search", parameters, null, cancellationToken).ConfigureAwait(false);
        var expandedResults = WingetTableParser.ParseSearchResults(expandedResult.Output);
        return expandedResults.FirstOrDefault(candidate => MatchesExpandedSearchResult(result, candidate, idQuery)) ?? result;
    }

    private async Task<WingetPackageDetails> LoadInstalledPackageDetailsAsync(string id, string? source, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["--id"] = id,
            ["--exact"] = null,
            ["--details"] = null,
            ["--accept-source-agreements"] = null
        };

        if (!string.IsNullOrWhiteSpace(source))
        {
            parameters["--source"] = source;
        }

        var result = await _wingetService.InvokeAsync("list", parameters, null, cancellationToken).ConfigureAwait(false);
        return ParsePackageDetails(result.Output);
    }

    private static SavedPackageResolutionResult ResolveUniqueSearchCandidate(
        IReadOnlyList<SearchResult> candidates,
        string originalId,
        string originalName,
        string originalSource,
        Func<SearchResult, bool> preferredMatch)
    {
        if (candidates.Count == 0)
        {
            return SavedPackageResolutionResult.Unresolved(originalId, originalName, originalSource);
        }

        var preferredCandidates = candidates.Where(preferredMatch).ToList();
        var resolutionCandidates = preferredCandidates.Count > 0 ? preferredCandidates : candidates;
        if (resolutionCandidates.Count != 1)
        {
            return SavedPackageResolutionResult.Ambiguous(originalId, originalName, originalSource);
        }

        var candidate = resolutionCandidates[0];
        return SavedPackageResolutionResult.Resolved(candidate.Id, candidate.Name, candidate.Source);
    }

    private static Dictionary<string, string?> CreatePackageLookupParameters(
        string option,
        string value,
        string source,
        bool exact)
    {
        var parameters = new Dictionary<string, string?>
        {
            [option] = value,
            ["--accept-source-agreements"] = null,
            ["--disable-interactivity"] = null
        };

        if (exact)
        {
            parameters["--exact"] = null;
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            parameters["--source"] = source;
        }

        return parameters;
    }

    private static bool IsAmbiguousPackageOutput(string output)
    {
        return output.Contains("Multiple packages found", StringComparison.OrdinalIgnoreCase)
            || output.Contains("Piu pacchetti trovati", StringComparison.OrdinalIgnoreCase)
            || output.Contains("Più pacchetti trovati", StringComparison.OrdinalIgnoreCase);
    }

    private static bool NeedsSearchResultExpansion(SearchResult result)
    {
        return HasTruncationMarker(result.Name) || HasTruncationMarker(result.Id);
    }

    private static bool HasTruncationMarker(string value)
    {
        return value.Contains('…') || value.Contains("...", StringComparison.Ordinal);
    }

    private static string GetSearchLookupPrefix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var ellipsisIndex = value.IndexOf('…');
        if (ellipsisIndex >= 0)
        {
            return value[..ellipsisIndex];
        }

        var dotsIndex = value.IndexOf("...", StringComparison.Ordinal);
        return dotsIndex >= 0
            ? value[..dotsIndex]
            : value;
    }

    private static bool MatchesExpandedSearchResult(SearchResult original, SearchResult candidate, string idQuery)
    {
        if (!candidate.Id.StartsWith(idQuery, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(original.Source) &&
            !string.Equals(candidate.Source, original.Source, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(original.Version) &&
            !string.Equals(original.Version, "Unknown", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(candidate.Version, original.Version, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var originalNamePrefix = GetSearchLookupPrefix(original.Name);
        if (!string.IsNullOrWhiteSpace(originalNamePrefix) &&
            !candidate.Name.StartsWith(originalNamePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static WingetPackageDetails ParsePackageDetails(string output)
    {
        var scope = string.Empty;
        var architecture = string.Empty;
        var locale = string.Empty;
        var installerType = string.Empty;
        foreach (var rawLine in output.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            var separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            if (MatchesAny(key, "Installed Scope", "Scope", "Ambito installato", "Ambito"))
            {
                scope = NormalizeScopeValue(value);
            }
            else if (MatchesAny(key, "Installed Architecture", "Architecture", "Architettura installata", "Architettura"))
            {
                architecture = NormalizeArchitectureValue(value);
            }
            else if (MatchesAny(key, "Installer Locale", "Locale programma di installazione"))
            {
                locale = value.Trim();
            }
            else if (MatchesAny(key, "Installer Type", "Tipo di programma di installazione"))
            {
                installerType = value.Trim();
            }
        }

        return new WingetPackageDetails
        {
            Scope = scope,
            Architecture = architecture,
            Locale = locale,
            InstallerType = installerType
        };
    }

    private static bool TryNormalizeScope(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (string.Equals(value, "Machine", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "machine";
            return true;
        }

        if (string.Equals(value, "User", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "user";
            return true;
        }

        return false;
    }

    private static bool TryNormalizeArchitecture(string? value, out string normalized)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            normalized = string.Empty;
            return false;
        }

        normalized = value.Trim().ToLowerInvariant();
        return normalized is "x64" or "x86" or "arm64" or "arm";
    }

    private static bool MatchesAny(string value, params string[] candidates)
    {
        return candidates.Any(candidate => string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeScopeValue(string value)
    {
        return TryNormalizeScope(value, out var normalized) ? normalized : value.Trim();
    }

    private static string NormalizeArchitectureValue(string value)
    {
        return TryNormalizeArchitecture(value, out var normalized) ? normalized : value.Trim();
    }
}
