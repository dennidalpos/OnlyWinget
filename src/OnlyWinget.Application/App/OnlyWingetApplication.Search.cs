using OnlyWinget.Application.Activity;
using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Operations;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Presets;

namespace OnlyWinget.Application.App;

public sealed partial class OnlyWingetApplication
{
    public async Task<ApplicationActionResult> SearchAsync(string query, CancellationToken callerCancellationToken)
    {
        return await RunAsync(
                ApplicationBusyState.Searching,
                callerCancellationToken,
                async cancellationToken =>
                {
                    RequireWinget();
                    var enabledSources = GetEnabledSourceNames();
                    if (enabledSources.Count == 0)
                    {
                        throw new InvalidOperationException("Enable at least one winget source before searching.");
                    }

                    searchResults.Clear();
                    var sourceErrors = new List<string>();
                    var searchTasks = enabledSources.Select(async source =>
                    {
                        var outcome = await packageSearch.SearchAsync(new PackageSearchRequest(query, source), cancellationToken)
                            .ConfigureAwait(false);
                        if (!outcome.Succeeded)
                        {
                            lock (sourceErrors)
                            {
                                sourceErrors.Add($"{source}: {outcome.Error?.Message ?? "winget search failed."}");
                            }
                        }
                        else
                        {
                            lock (searchResults)
                            {
                                searchResults.AddRange(outcome.Rows);
                            }
                        }
                    }).ToArray();
                    await Task.WhenAll(searchTasks).ConfigureAwait(false);

                    if (searchResults.Count == 0 && sourceErrors.Count > 0)
                    {
                        throw new InvalidOperationException(string.Join(Environment.NewLine, sourceErrors));
                    }

                    var distinctResults = searchResults
                        .DistinctBy(result => result.Package)
                        .OrderBy(result => result.Name, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(result => result.Package.Id, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    searchResults.Clear();
                    searchResults.AddRange(distinctResults);
                    var metadataFailureCount = await RefreshPackageMetadataAsync(
                            searchResults.Select(result => result.Package),
                            cancellationToken)
                        .ConfigureAwait(false);
                    searchSelection.ReplaceAvailable(searchResults.Select(result => result.Package));
                    AddActivity(ActivitySeverity.Information, "Search completed", $"{searchResults.Count} result(s).");
                    if (sourceErrors.Count > 0)
                    {
                        AddActivity(
                            ActivitySeverity.Warning,
                            "Some sources could not be searched",
                            string.Join(Environment.NewLine, sourceErrors));
                    }

                    if (metadataFailureCount > 0)
                    {
                        AddActivity(
                            ActivitySeverity.Warning,
                            "Some package publishers could not be resolved",
                            $"{metadataFailureCount} package(s).");
                    }
                },
                "Unable to search packages.")
            .ConfigureAwait(false);
    }

    public ApplicationActionResult ToggleSearchResult(PackageIdentity package) => ToggleSelection(searchSelection, package);

    public ApplicationActionResult ToggleAllSearchResults() => Run(searchSelection.ToggleAll);

    public ApplicationActionResult SetSearchResultsSelection(IEnumerable<PackageIdentity> packages, bool isSelected) =>
        Run(() => { foreach (var p in packages) searchSelection.SetSelected(p, isSelected); });

    public async Task<ApplicationActionResult> AddSelectedSearchResultsToActivePresetAsync(CancellationToken callerCancellationToken)
    {
        return await RunAsync(
                ApplicationBusyState.Searching,
                callerCancellationToken,
                async cancellationToken =>
                {
                    RequireWinget();
                    var active = EnsureActivePreset();
                    var packages = active.Packages.ToList();
                    var added = 0;
                    foreach (var selected in searchSelection.Selected)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var resolution = await ValidatePackageAsync(selected, cancellationToken).ConfigureAwait(false);
                        var package = resolution.Package;
                        if (packages.Contains(package))
                        {
                            continue;
                        }

                        packages.Add(package);
                        added++;
                    }

                    ReplacePreset(active.Name, new Preset(active.Name, packages), active.Name);
                    AddActivity(ActivitySeverity.Success, "Search packages added", $"{added} package(s).");
                },
                "Unable to add selected packages.")
            .ConfigureAwait(false);
    }

    public async Task<ApplicationActionResult> InstallSelectedSearchResultsDirectAsync(
        CancellationToken callerCancellationToken,
        IProgress<OperationProgress>? progress = null)
    {
        RequireWinget();
        var selected = searchSelection.Selected.ToArray();
        if (selected.Length == 0)
        {
            return ApplicationActionResult.Failure("Select at least one package to install.");
        }

        var selections = selected.Select(p => new PackageSelection(p, PackageAction.Install)).ToArray();
        var plan = new OperationPlan("Direct Search Install", selections);
        return await ExecutePlanAsync(plan, callerCancellationToken, progress).ConfigureAwait(false);
    }

    public async Task<ApplicationActionResult> RefreshWorkspacePackageMetadataAsync(CancellationToken callerCancellationToken)
    {
        return await RunAsync(
                ApplicationBusyState.ValidatingPackages,
                callerCancellationToken,
                async cancellationToken =>
                {
                    RequireWinget();
                    var packages = workspace.Presets
                        .SelectMany(preset => preset.Packages)
                        .Distinct()
                        .ToArray();
                    await RefreshPackageMetadataAsync(packages, cancellationToken).ConfigureAwait(false);

                    int count;
                    lock (packageMetadata)
                    {
                        count = packageMetadata.Count;
                    }
                    AddActivity(ActivitySeverity.Information, "Package metadata refreshed", $"{count} package(s).");
                },
                "Unable to refresh package metadata.")
            .ConfigureAwait(false);
    }

    public PackageResolution? GetPackageMetadata(PackageIdentity package)
    {
        lock (packageMetadata)
        {
            return packageMetadata.TryGetValue(package, out var cached) ? cached.Resolution : null;
        }
    }

    private Dictionary<PackageIdentity, PackageResolution> SnapshotPackageMetadata()
    {
        lock (packageMetadata)
        {
            return packageMetadata.ToDictionary(pair => pair.Key, pair => pair.Value.Resolution);
        }
    }

    private async Task<PackageResolution> ValidatePackageAsync(
        PackageIdentity package,
        CancellationToken cancellationToken)
    {
        var enabledSources = GetEnabledSourceNames();
        if (enabledSources.Count == 0)
        {
            throw new InvalidOperationException("Enable at least one winget source before adding packages.");
        }

        if (package.Source is { } requestedSource)
        {
            if (!enabledSources.Contains(requestedSource, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Source '{requestedSource}' is disabled or unavailable.");
            }

            var resolution = await packageResolver.ResolveAsync(package, cancellationToken).ConfigureAwait(false);
            if (!resolution.IsResolved)
            {
                throw new InvalidOperationException(resolution.Error?.Message ?? $"Package '{package.Id}' was not found in source '{requestedSource}'.");
            }

            lock (packageMetadata)
            {
                packageMetadata[resolution.Package] = new CachedPackageResolution(resolution, clock.GetUtcNow());
            }
            return resolution;
        }

        var matches = new List<PackageResolution>();
        using var semaphore = new SemaphoreSlim(4);
        var resolveTasks = enabledSources.Select(async source =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var resolution = await packageResolver.ResolveAsync(
                        new PackageIdentity(package.Id, source),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (resolution.IsResolved)
                {
                    lock (matches)
                    {
                        matches.Add(resolution);
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        }).ToArray();
        await Task.WhenAll(resolveTasks).ConfigureAwait(false);

        if (matches.Count == 0)
        {
            throw new InvalidOperationException($"Package '{package.Id}' was not found in any enabled source.");
        }

        if (matches.Count > 1)
        {
            throw new InvalidOperationException($"Package '{package.Id}' exists in multiple enabled sources. Specify a source.");
        }

        var match = matches[0];
        lock (packageMetadata)
        {
            packageMetadata[match.Package] = new CachedPackageResolution(match, clock.GetUtcNow());
        }
        return match;
    }

    private async Task<int> RefreshPackageMetadataAsync(
        IEnumerable<PackageIdentity> packages,
        CancellationToken cancellationToken)
    {
        var unresolvedCount = 0;
        var distinctPackages = packages
            .Distinct()
            .Where(package =>
            {
                lock (packageMetadata)
                {
                    return !packageMetadata.TryGetValue(package, out var cached) ||
                        clock.GetUtcNow() - cached.ResolvedAt >= PackageMetadataCacheDuration;
                }
            })
            .ToArray();

        using var semaphore = new SemaphoreSlim(4);
        var tasks = distinctPackages.Select(async package =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var resolution = await packageResolver.ResolveAsync(package, cancellationToken).ConfigureAwait(false);
                if (resolution.IsResolved)
                {
                    var cached = new CachedPackageResolution(resolution, clock.GetUtcNow());
                    lock (packageMetadata)
                    {
                        packageMetadata[package] = cached;
                        packageMetadata[resolution.Package] = cached;
                    }
                }
                else
                {
                    Interlocked.Increment(ref unresolvedCount);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                ExceptionLogger?.Invoke(exception.Message, exception);
                Interlocked.Increment(ref unresolvedCount);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return unresolvedCount;
    }
}
