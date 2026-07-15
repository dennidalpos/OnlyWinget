using OnlyWinget.Application.Activity;
using OnlyWinget.Application.Operations;
using OnlyWinget.Application.Presets;
using OnlyWinget.Application.Storage;
using OnlyWinget.Application.System;
using OnlyWinget.Application.Winget;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Domain.Operations;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Presets;
using OnlyWinget.Domain.Selection;

namespace OnlyWinget.Application.App;

public sealed class OnlyWingetApplication(
    IWorkspaceStore workspaceStore,
    ISystemCapabilityService capabilityService,
    IPackageSearchService packageSearch,
    IPackageResolver packageResolver,
    IUpdateLoader updateLoader,
    IWindowsUpdateService windowsUpdateService,
    IWingetSourceService sourceService,
    IOperationExecutor operationExecutor,
    TimeProvider? timeProvider = null,
    ISourcePreferenceStore? sourcePreferenceStore = null)
{
    public bool ContinueOperationsAfterFailure { get; set; }
    public Action<string, Exception>? ExceptionLogger { get; set; }
    public Action<AppLogLevel, string, string>? Logger { get; set; }

    private readonly PresetDocumentService presetDocuments = new();
    private readonly OperationPlanner operationPlanner = new();
    private readonly SelectionState<PackageIdentity> presetSelection = new();
    private readonly SelectionState<PackageIdentity> presetInstallSelection = new();
    private readonly SelectionState<PackageIdentity> searchSelection = new();
    private readonly SelectionState<PackageIdentity> updateSelection = new();
    private readonly SelectionState<WindowsUpdateIdentity> windowsUpdateSelection = new();
    private readonly List<PackageSearchResult> searchResults = [];
    private readonly List<PackageUpdate> updates = [];
    private readonly List<WindowsUpdateItem> windowsUpdates = [];
    private readonly List<WingetSource> sources = [];
    private readonly List<ActivityEntry> activity = [];
    private readonly List<OperationExecutionResult> lastOperationResults = [];
    private readonly Dictionary<PackageIdentity, PackageResolution> packageMetadata = new();
    private readonly List<WindowsUpdateInstallResult> lastWindowsUpdateResults = [];
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly ISourcePreferenceStore sourcePreferences = sourcePreferenceStore ?? new EmptySourcePreferenceStore();
    private readonly HashSet<string> disabledSources = new(StringComparer.OrdinalIgnoreCase);

    private bool defaultSourcesConfigured;
    private WorkspaceState workspace = WorkspaceState.Empty;
    private ApplicationBusyState busyState;
    private SystemCapabilities capabilities = SystemCapabilities.Unknown;
    private ClassifiedWingetError? sourceError;
    private string? userVisibleError;
    private OperationProgress? operationProgress;
    private int operationInProgress;

    public OnlyWingetState State => CreateState();

    public event EventHandler? StateChanged;

    public async Task<ApplicationActionResult> LoadWorkspaceAsync(CancellationToken cancellationToken)
    {
        return await RunAsync(
                ApplicationBusyState.LoadingWorkspace,
                async () =>
                {
                    workspace = NormalizeWorkspace(await workspaceStore.LoadAsync(cancellationToken).ConfigureAwait(false));
                    var preferences = await sourcePreferences.LoadAsync(cancellationToken).ConfigureAwait(false);
                    disabledSources.Clear();
                    disabledSources.UnionWith(preferences.DisabledSources);
                    defaultSourcesConfigured = preferences.DefaultSourcesConfigured;
                    RefreshPresetSelection();
                    AddActivity(ActivitySeverity.Success, "Workspace loaded", "Workspace state is ready.");
                },
                "Unable to load workspace.")
            .ConfigureAwait(false);
    }

    public async Task<ApplicationActionResult> SaveWorkspaceAsync(CancellationToken cancellationToken)
    {
        return await RunAsync(
                ApplicationBusyState.SavingWorkspace,
                async () =>
                {
                    await workspaceStore.SaveAsync(workspace, cancellationToken).ConfigureAwait(false);
                    AddActivity(ActivitySeverity.Success, "Workspace saved", "Workspace state was saved.");
                },
                "Unable to save workspace.")
            .ConfigureAwait(false);
    }

    public async Task<ApplicationActionResult> RefreshCapabilitiesAsync(CancellationToken cancellationToken)
    {
        return await RunAsync(
                ApplicationBusyState.CheckingCapabilities,
                async () =>
                {
                    capabilities = await capabilityService.GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
                    AddActivity(
                        capabilities.CanUseWinget ? ActivitySeverity.Success : ActivitySeverity.Error,
                        "System capabilities checked",
                        capabilities.CanUseWinget ? "winget is available." : capabilities.WingetUnavailableMessage);
                },
                "Unable to check system capabilities.")
            .ConfigureAwait(false);
    }

    public ApplicationActionResult AddPreset(string name) =>
        Run(() =>
        {
            var preset = new Preset(name, []);
            if (FindPreset(preset.Name) is not null)
            {
                throw new InvalidOperationException("A preset with the same name already exists.");
            }

            workspace = NormalizeWorkspace(new WorkspaceState([.. workspace.Presets, preset], preset.Name));
            RefreshPresetSelection();
            AddActivity(ActivitySeverity.Success, "Preset added", preset.Name);
        });

    public ApplicationActionResult RenameActivePreset(string name) =>
        Run(() =>
        {
            var active = RequireActivePreset();
            var renamed = new Preset(name, active.Packages);
            if (!string.Equals(active.Name, renamed.Name, StringComparison.OrdinalIgnoreCase) &&
                FindPreset(renamed.Name) is not null)
            {
                throw new InvalidOperationException("A preset with the same name already exists.");
            }

            ReplacePreset(active.Name, renamed, renamed.Name);
            AddActivity(ActivitySeverity.Success, "Preset renamed", $"{active.Name} -> {renamed.Name}");
        });

    public ApplicationActionResult RemoveActivePreset() =>
        Run(() =>
        {
            var active = RequireActivePreset();
            var remaining = workspace.Presets
                .Where(preset => !PresetNameEquals(preset.Name, active.Name))
                .ToArray();

            workspace = NormalizeWorkspace(new WorkspaceState(remaining, remaining.FirstOrDefault()?.Name));
            RefreshPresetSelection();
            AddActivity(ActivitySeverity.Success, "Preset removed", active.Name);
        });

    public ApplicationActionResult SetActivePreset(string name) =>
        Run(() =>
        {
            var preset = FindPreset(name) ?? throw new InvalidOperationException("Preset was not found.");
            workspace = new WorkspaceState(workspace.Presets, preset.Name);
            RefreshPresetSelection();
        });

    public async Task<ApplicationActionResult> AddPackageToActivePresetAsync(
        PackageIdentity package,
        CancellationToken cancellationToken) =>
        await RunAsync(ApplicationBusyState.ValidatingPackages, async () =>
        {
            ArgumentNullException.ThrowIfNull(package);
            var validated = await ValidatePackageAsync(package, cancellationToken).ConfigureAwait(false);
            var active = RequireActivePreset();
            if (active.Packages.Contains(validated.Package))
            {
                throw new InvalidOperationException("Package is already in the active preset.");
            }

            ReplacePreset(active.Name, new Preset(active.Name, [.. active.Packages, validated.Package]), active.Name);
            AddActivity(ActivitySeverity.Success, "Package added", validated.Package.Id);
        }, "Unable to validate the package.").ConfigureAwait(false);

    public async Task<ApplicationActionResult> ReplacePackageInActivePresetAsync(
        PackageIdentity current,
        PackageIdentity replacement,
        CancellationToken cancellationToken) =>
        await RunAsync(ApplicationBusyState.ValidatingPackages, async () =>
        {
            ArgumentNullException.ThrowIfNull(current);
            ArgumentNullException.ThrowIfNull(replacement);
            var validated = await ValidatePackageAsync(replacement, cancellationToken).ConfigureAwait(false);
            var active = RequireActivePreset();
            if (!active.Packages.Contains(current))
            {
                throw new InvalidOperationException("Package was not found in the active preset.");
            }

            if (current != validated.Package && active.Packages.Contains(validated.Package))
            {
                throw new InvalidOperationException("Package is already in the active preset.");
            }

            var packages = active.Packages
                .Select(package => package == current ? validated.Package : package)
                .ToArray();
            ReplacePreset(active.Name, new Preset(active.Name, packages), active.Name);
            AddActivity(ActivitySeverity.Success, "Package updated", validated.Package.Id);
        }, "Unable to validate the package.").ConfigureAwait(false);

    public ApplicationActionResult RemoveSelectedPackagesFromActivePreset() =>
        Run(() =>
        {
            var active = RequireActivePreset();
            var selected = presetInstallSelection.Selected.ToArray();
            if (selected.Length == 0)
            {
                throw new InvalidOperationException("Select at least one package to remove.");
            }

            var packages = active.Packages
                .Where(package => !selected.Contains(package))
                .ToArray();
            ReplacePreset(active.Name, new Preset(active.Name, packages), active.Name);
            AddActivity(ActivitySeverity.Success, "Packages removed", selected.Length.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
        });

    public ApplicationActionResult TogglePresetPackage(PackageIdentity package) => ToggleSelection(presetInstallSelection, package);

    public ApplicationActionResult TogglePresetPackageInclusion(PackageIdentity package) => ToggleSelection(presetInstallSelection, package);

    public ApplicationActionResult ToggleAllPresetPackages() => Run(presetInstallSelection.ToggleAll);

    public ApplicationActionResult SelectPresetPackage(PackageIdentity package) =>
        Run(() =>
        {
            presetInstallSelection.ClearSelection();
            presetInstallSelection.SetSelected(package, true);
        });

    public async Task<ApplicationActionResult> SearchAsync(string query, CancellationToken cancellationToken)
    {
        return await RunAsync(
                ApplicationBusyState.Searching,
                async () =>
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

    public async Task<ApplicationActionResult> AddSelectedSearchResultsToActivePresetAsync(CancellationToken cancellationToken)
    {
        return await RunAsync(
                ApplicationBusyState.Searching,
                async () =>
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

    public async Task<ApplicationActionResult> RefreshUpdatesAsync(CancellationToken cancellationToken)
    {
        return await RunAsync(
                ApplicationBusyState.RefreshingUpdates,
                async () =>
                {
                    RequireWinget();
                    var enabledSources = GetEnabledSourceNames();
                    if (enabledSources.Count == 0)
                    {
                        throw new InvalidOperationException("Enable at least one winget source before refreshing updates.");
                    }

                    updates.Clear();
                    lastOperationResults.Clear();
                    var sourceErrors = new List<string>();
                    var loadTasks = enabledSources.Select(async source =>
                    {
                        var outcome = await updateLoader.LoadUpdatesAsync(source, cancellationToken).ConfigureAwait(false);
                        if (!outcome.Succeeded && outcome.Error?.Kind != WingetErrorKind.NoUpdates)
                        {
                            lock (sourceErrors)
                            {
                                sourceErrors.Add($"{source}: {outcome.Error?.Message ?? "winget upgrade failed."}");
                            }
                        }
                        else
                        {
                            lock (updates)
                            {
                                updates.AddRange(outcome.Rows);
                            }
                        }
                    }).ToArray();
                    await Task.WhenAll(loadTasks).ConfigureAwait(false);

                    if (updates.Count == 0 && sourceErrors.Count > 0)
                    {
                        throw new InvalidOperationException(string.Join(Environment.NewLine, sourceErrors));
                    }

                    var distinctUpdates = updates
                        .DistinctBy(update => update.Package)
                        .OrderBy(update => update.Name, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    updates.Clear();
                    updates.AddRange(distinctUpdates);

                    await RefreshPackageMetadataAsync(
                            updates.Select(update => update.Package),
                            cancellationToken)
                        .ConfigureAwait(false);

                    updateSelection.ReplaceAvailable(updates.Select(update => update.Package));
                    AddActivity(ActivitySeverity.Information, "Updates refreshed", $"{updates.Count} update(s).");
                    if (sourceErrors.Count > 0)
                    {
                        AddActivity(
                            ActivitySeverity.Warning,
                            "Some sources could not be refreshed",
                            string.Join(Environment.NewLine, sourceErrors));
                    }
                },
                "Unable to refresh updates.")
            .ConfigureAwait(false);
    }

    public async Task<ApplicationActionResult> ScanWindowsUpdatesAsync(
        WindowsUpdateOptions options,
        CancellationToken cancellationToken)
    {
        return await RunAsync(
                ApplicationBusyState.ScanningWindowsUpdates,
                async () =>
                {
                    RequireWindowsUpdate();
                    lastWindowsUpdateResults.Clear();
                    var outcome = await windowsUpdateService.ScanAsync(options, cancellationToken).ConfigureAwait(false);
                    if (!outcome.Succeeded)
                    {
                        throw new InvalidOperationException(outcome.Error?.Message ?? "Windows Update scan failed.");
                    }

                    windowsUpdates.Clear();
                    windowsUpdates.AddRange(outcome.Rows
                        .DistinctBy(update => WindowsUpdateFingerprint(update.Identity))
                        .OrderBy(update => update.Title, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(update => update.Identity.UpdateId, StringComparer.OrdinalIgnoreCase));
                    windowsUpdateSelection.ReplaceAvailable(windowsUpdates.Select(update => update.Identity));
                    AddActivity(ActivitySeverity.Information, "Windows Update scan completed", $"{windowsUpdates.Count} update(s).");
                },
                "Unable to scan Windows Update.")
            .ConfigureAwait(false);
    }

    public ApplicationActionResult ToggleWindowsUpdate(WindowsUpdateIdentity update) =>
        ToggleSelection(windowsUpdateSelection, update);

    public ApplicationActionResult ToggleAllWindowsUpdates() => Run(windowsUpdateSelection.ToggleAll);

    public async Task<ApplicationActionResult> InstallSelectedWindowsUpdatesAsync(
        WindowsUpdateOptions options,
        CancellationToken cancellationToken)
    {
        var selected = windowsUpdateSelection.Selected.ToArray();
        return await RunAsync(
                ApplicationBusyState.InstallingWindowsUpdates,
                async () =>
                {
                    RequireWindowsUpdate();
                    if (selected.Length == 0)
                    {
                        throw new InvalidOperationException("Select at least one Windows update before installing.");
                    }

                    lastWindowsUpdateResults.Clear();
                    AddActivity(ActivitySeverity.Information, "Windows Update install started", $"{selected.Length} update(s).");
                    var outcome = await windowsUpdateService.InstallAsync(selected, options, cancellationToken).ConfigureAwait(false);
                    if (!outcome.Succeeded)
                    {
                        throw new InvalidOperationException(outcome.Error?.Message ?? "Windows Update install failed.");
                    }

                    lastWindowsUpdateResults.AddRange(outcome.Rows);
                    foreach (var result in outcome.Rows)
                    {
                        var severity = result.Succeeded ? ActivitySeverity.Success : ActivitySeverity.Error;
                        var logMessage = result.Succeeded
                            ? "Completed."
                            : (string.IsNullOrWhiteSpace(result.Message) ? $"Result Code: {result.ResultCode}" : $"{result.Message} (Result Code: {result.ResultCode})");
                        AddActivity(severity, result.Title, logMessage);
                        Logger?.Invoke(
                            AppLogLevel.Verbose,
                            $"[Windows Update Result] Title: {result.Title}, Succeeded: {result.Succeeded}, ResultCode: {result.ResultCode}, Message: {result.Message}",
                            nameof(InstallSelectedWindowsUpdatesAsync));
                    }

                    if (outcome.Rows.Any(result => !result.Succeeded))
                    {
                        throw new InvalidOperationException("One or more Windows updates failed.");
                    }

                    if (outcome.Rows.Any(result => result.RebootRequired))
                    {
                        AddActivity(ActivitySeverity.Information, "Restart required", "One or more Windows updates require a restart.");
                    }
                },
                "Unable to install Windows updates.")
            .ConfigureAwait(false);
    }

    public async Task<ApplicationActionResult> RefreshSourcesAsync(CancellationToken cancellationToken)
    {
        return await RunAsync(
                ApplicationBusyState.ManagingSources,
                async () =>
                {
                    RequireWinget();
                    await EnsureOfficialSourcesConfiguredAsync(cancellationToken).ConfigureAwait(false);
                    var outcome = await sourceService.ListSourcesAsync(cancellationToken).ConfigureAwait(false);
                    ApplySourceOutcome(outcome, updateRows: true);
                    AddActivity(ActivitySeverity.Information, "Sources refreshed", $"{sources.Count} source(s).");
                },
                "Unable to refresh winget sources.")
            .ConfigureAwait(false);
    }

    public async Task<ApplicationActionResult> RefreshWorkspacePackageMetadataAsync(CancellationToken cancellationToken)
    {
        return await RunAsync(
                ApplicationBusyState.ValidatingPackages,
                async () =>
                {
                    RequireWinget();
                    var packages = workspace.Presets
                        .SelectMany(preset => preset.Packages)
                        .Distinct()
                        .ToArray();
                    await RefreshPackageMetadataAsync(packages, cancellationToken).ConfigureAwait(false);

                    AddActivity(ActivitySeverity.Information, "Package metadata refreshed", $"{packageMetadata.Count} package(s).");
                },
                "Unable to refresh package metadata.")
            .ConfigureAwait(false);
    }

    public async Task<ApplicationActionResult> UpdateSourcesAsync(CancellationToken cancellationToken)
    {
        return await RunSourceMutationAsync(
                () => sourceService.UpdateSourcesAsync(cancellationToken),
                "Sources updated",
                "winget source update completed.",
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ApplicationActionResult> AddSourceAsync(
        string name,
        string argument,
        CancellationToken cancellationToken)
    {
        return await RunSourceMutationAsync(
                () => sourceService.AddSourceAsync(name, argument, cancellationToken),
                "Source added",
                name,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ApplicationActionResult> RemoveSourceAsync(string name, CancellationToken cancellationToken)
    {
        return await RunSourceMutationAsync(
                () => sourceService.RemoveSourceAsync(name, cancellationToken),
                "Source removed",
                name,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ApplicationActionResult> ResetSourcesAsync(CancellationToken cancellationToken)
    {
        var result = await RunSourceMutationAsync(
                () => sourceService.ResetSourcesAsync(cancellationToken),
                "Sources reset",
                "winget sources reset to defaults.",
                cancellationToken)
            .ConfigureAwait(false);
        if (result.Succeeded)
        {
            disabledSources.Clear();
            defaultSourcesConfigured = true;
            ApplySourcePreferences();
            await sourcePreferences.SaveAsync(new SourcePreferences([], DefaultSourcesConfigured: true), cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    public async Task<ApplicationActionResult> SetSourceEnabledAsync(
        string name,
        bool isEnabled,
        CancellationToken cancellationToken)
    {
        return await RunAsync(
                ApplicationBusyState.ManagingSources,
                async () =>
                {
                    RequireWinget();
                    if (!sources.Any(source => string.Equals(source.Name, name, StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new InvalidOperationException("The winget source was not found.");
                    }

                    if (isEnabled)
                    {
                        disabledSources.Remove(name);
                    }
                    else
                    {
                        disabledSources.Add(name);
                    }

                    await sourcePreferences.SaveAsync(
                            new SourcePreferences(disabledSources.ToArray()),
                            cancellationToken)
                        .ConfigureAwait(false);
                    ApplySourcePreferences();
                    AddActivity(ActivitySeverity.Information, "Source preference changed", $"{name}: {(isEnabled ? "enabled" : "disabled")}");
                },
                "Unable to save the source preference.")
            .ConfigureAwait(false);
    }

    public ApplicationActionResult ToggleUpdate(PackageIdentity package) => ToggleSelection(updateSelection, package);

    public ApplicationActionResult ToggleAllUpdates() => Run(updateSelection.ToggleAll);

    public async Task<ApplicationActionResult> ApplySelectedUpdatesAsync(
        CancellationToken cancellationToken,
        IProgress<OperationProgress>? progress = null)
    {
        var selections = updateSelection.Selected
            .Select(package => new PackageSelection(package, PackageAction.Upgrade))
            .ToArray();
        return await ExecutePlanAsync(new OperationPlan("Selected updates", selections), cancellationToken, progress)
            .ConfigureAwait(false);
    }

    public async Task<ApplicationActionResult> ApplyActivePresetAsync(
        PackageAction action,
        CancellationToken cancellationToken,
        IProgress<OperationProgress>? progress = null)
    {
        var active = RequireActivePreset();
        var includedPackages = active.Packages
            .Where(package => presetInstallSelection.Selected.Contains(package))
            .ToArray();
        var plan = operationPlanner.CreatePresetPlan(new Preset(active.Name, includedPackages), action);
        return await ExecutePlanAsync(plan, cancellationToken, progress).ConfigureAwait(false);
    }

    public ApplicationActionResult ClearActivity() =>
        Run(() =>
        {
            activity.Clear();
            userVisibleError = null;
        });

    public ApplicationActionResult RestoreActivity(IEnumerable<ActivityEntry> entries) =>
        Run(() =>
        {
            ArgumentNullException.ThrowIfNull(entries);
            activity.Clear();
            activity.AddRange(entries);
            userVisibleError = null;
        });

    public ApplicationActionResult ReportExternalFailure(string message) =>
        Run(() => throw new InvalidOperationException(
            string.IsNullOrWhiteSpace(message) ? "External operation failed." : message.Trim()));

    public string ExportActivePreset()
    {
        var active = RequireActivePreset();
        return presetDocuments.Export(active);
    }

    public async Task<ApplicationActionResult> ImportPresetAsync(string json, CancellationToken cancellationToken) =>
        await RunAsync(ApplicationBusyState.ValidatingPackages, async () =>
        {
            var preset = presetDocuments.Import(json);
            if (FindPreset(preset.Name) is not null)
            {
                throw new InvalidOperationException("A preset with the same name already exists.");
            }

            var validatedPackages = new List<PackageIdentity>();
            foreach (var package in preset.Packages)
            {
                var validated = await ValidatePackageAsync(package, cancellationToken).ConfigureAwait(false);
                validatedPackages.Add(validated.Package);
            }

            var validatedPreset = new Preset(preset.Name, validatedPackages);
            workspace = NormalizeWorkspace(new WorkspaceState([.. workspace.Presets, validatedPreset], validatedPreset.Name));
            RefreshPresetSelection();
            AddActivity(ActivitySeverity.Success, "Preset imported", validatedPreset.Name);
        }, "Unable to import and validate the preset.").ConfigureAwait(false);

    private async Task<ApplicationActionResult> ExecutePlanAsync(
        OperationPlan plan,
        CancellationToken cancellationToken,
        IProgress<OperationProgress>? progress)
    {
        return await RunAsync(
                ApplicationBusyState.ExecutingOperation,
                async () =>
                {
                    RequireWinget();
                    if (!plan.HasWork)
                    {
                        throw new InvalidOperationException("Select at least one package before applying an operation.");
                    }

                    var validatedSelections = new List<PackageSelection>();
                    var validationFailures = new List<OperationExecutionResult>();
                    var skippedResults = new List<OperationExecutionResult>();

                    foreach (var selection in plan.Selections)
                    {
                        try
                        {
                            var validated = await ValidatePackageAsync(selection.Package, cancellationToken).ConfigureAwait(false);

                            // Preventative check
                            if (selection.Action is PackageAction.Install or PackageAction.Upgrade)
                            {
                                var installedStatus = await packageResolver.CheckInstalledStatusAsync(validated.Package, cancellationToken).ConfigureAwait(false);
                                if (installedStatus.IsInstalled)
                                {
                                    bool skip = false;
                                    string skipMessage = string.Empty;

                                    if (selection.Action == PackageAction.Install)
                                    {
                                        skip = true;
                                        skipMessage = $"Package is already present (Installed: {installedStatus.InstalledVersion}).";
                                    }
                                    else if (selection.Action == PackageAction.Upgrade)
                                    {
                                        if (IsUpToDate(installedStatus.InstalledVersion, validated.Version))
                                        {
                                            skip = true;
                                            skipMessage = $"Package is already updated (Installed: {installedStatus.InstalledVersion}, Available: {validated.Version}).";
                                        }
                                    }

                                    if (skip)
                                    {
                                        var resultRow = new WingetCommandResult(0, skipMessage, string.Empty);
                                        var executionResult = new OperationExecutionResult(
                                            new PackageSelection(validated.Package, selection.Action),
                                            resultRow,
                                            null);
                                        skippedResults.Add(executionResult);
                                        continue;
                                    }
                                }
                            }

                            validatedSelections.Add(new PackageSelection(validated.Package, selection.Action));
                        }
                        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
                        {
                            if (!ContinueOperationsAfterFailure)
                            {
                                throw;
                            }
                            var error = new ClassifiedWingetError(WingetErrorKind.Unknown, exception.Message);
                            var dummyResult = new WingetCommandResult(-1, string.Empty, exception.Message);
                            validationFailures.Add(new OperationExecutionResult(selection, dummyResult, error));
                        }
                    }

                    var validatedPlan = new OperationPlan(plan.Name, validatedSelections);
                    lastOperationResults.Clear();
                    lastOperationResults.AddRange(validationFailures);
                    lastOperationResults.AddRange(skippedResults);

                    foreach (var result in validationFailures)
                    {
                        AddActivity(ActivitySeverity.Error, result.Selection.Package.Id, result.Error?.Message ?? "Validation failed.");
                    }

                    foreach (var result in skippedResults)
                    {
                        AddActivity(ActivitySeverity.Success, result.Selection.Package.Id, CreateOperationActivityMessage(result));
                    }

                    if (validatedSelections.Count > 0)
                    {
                        AddActivity(ActivitySeverity.Information, "Operation started", plan.Name);
                        operationProgress = new OperationProgress(string.Empty, WingetProgressPhase.Starting, 0, 0, plan.Selections.Count);
                        var forwardingProgress = new InlineProgress<OperationProgress>(update =>
                        {
                            operationProgress = update;
                            progress?.Report(update);
                            NotifyStateChanged();
                        });
                        var summary = await operationExecutor.ExecuteAsync(
                            validatedPlan,
                            cancellationToken,
                            forwardingProgress,
                            ContinueOperationsAfterFailure).ConfigureAwait(false);
                        lastOperationResults.AddRange(summary.Results);
                        foreach (var result in summary.Results)
                        {
                            var severity = result.Error?.Kind == WingetErrorKind.NoUpdates
                                ? ActivitySeverity.Warning
                                : (result.Succeeded ? ActivitySeverity.Success : ActivitySeverity.Error);
                            var message = CreateOperationActivityMessage(result);
                            AddActivity(severity, result.Selection.Package.Id, string.IsNullOrWhiteSpace(message) ? "Completed." : message);
                            Logger?.Invoke(
                                AppLogLevel.Verbose,
                                $"[Package Result] ID: {result.Selection.Package.Id}, Action: {result.Selection.Action}, Succeeded: {result.Succeeded}, ExitCode: {result.CommandResult.ExitCode}, StdOut: {result.CommandResult.StandardOutput.Trim()}, StdErr: {result.CommandResult.StandardError.Trim()}",
                                nameof(ApplySelectedUpdatesAsync));
                        }

                        var succeededPackages = summary.Results
                            .Concat(skippedResults)
                            .Where(result => result.Succeeded)
                            .Select(result => result.Selection.Package)
                            .ToArray();
                        updates.RemoveAll(update => succeededPackages.Contains(update.Package));
                        updateSelection.ReplaceAvailable(updates.Select(update => update.Package));

                        if (!summary.Succeeded || validationFailures.Count > 0)
                        {
                            throw new InvalidOperationException("One or more winget operations failed.");
                        }

                        operationProgress = operationProgress with { Phase = WingetProgressPhase.Completed, Percentage = 100, CompletedPackages = plan.Selections.Count };
                        progress?.Report(operationProgress);
                    }
                    else
                    {
                        var succeededPackages = skippedResults
                            .Where(result => result.Succeeded)
                            .Select(result => result.Selection.Package)
                            .ToArray();
                        updates.RemoveAll(update => succeededPackages.Contains(update.Package));
                        updateSelection.ReplaceAvailable(updates.Select(update => update.Package));

                        if (validationFailures.Count > 0)
                        {
                            throw new InvalidOperationException("One or more winget operations failed.");
                        }
                    }
                },
                "Unable to complete the operation.")
            .ConfigureAwait(false);
    }

    private OnlyWingetState CreateState()
    {
        var active = ActivePreset;
        return new OnlyWingetState(
            workspace,
            active,
            presetInstallSelection.Selected.ToArray(),
            presetInstallSelection.Selected.ToArray(),
            presetInstallSelection.HeaderState,
            searchResults.ToArray(),
            searchSelection.Selected.ToArray(),
            searchSelection.HeaderState,
            updates.ToArray(),
            updateSelection.Selected.ToArray(),
            updateSelection.HeaderState,
            windowsUpdates.ToArray(),
            windowsUpdateSelection.Selected.ToArray(),
            windowsUpdateSelection.HeaderState,
            lastWindowsUpdateResults.ToArray(),
            SnapshotPackageMetadata(),
            capabilities,
            sources.OrderBy(source => source.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
            sourceError,
            activity.ToArray(),
            lastOperationResults.ToArray(),
            operationProgress,
            busyState,
            userVisibleError);
    }

    private async Task<ApplicationActionResult> RunSourceMutationAsync(
        Func<Task<WingetOperationOutcome<WingetSource>>> operation,
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        return await RunAsync(
                ApplicationBusyState.ManagingSources,
                async () =>
                {
                    RequireWinget();
                    var outcome = await operation().ConfigureAwait(false);
                    ApplySourceOutcome(outcome, updateRows: false);
                    AddActivity(ActivitySeverity.Success, title, message);

                    var refresh = await sourceService.ListSourcesAsync(cancellationToken).ConfigureAwait(false);
                    ApplySourceOutcome(refresh, updateRows: true);
                    await sourcePreferences.SaveAsync(
                            new SourcePreferences(disabledSources.ToArray(), defaultSourcesConfigured),
                            cancellationToken)
                        .ConfigureAwait(false);
                },
                "Unable to manage winget sources.")
            .ConfigureAwait(false);
    }

    private void ApplySourceOutcome(WingetOperationOutcome<WingetSource> outcome, bool updateRows)
    {
        if (!outcome.Succeeded)
        {
            sourceError = outcome.Error;
            throw new InvalidOperationException(outcome.Error?.Message ?? "winget source failed.");
        }

        sourceError = null;
        if (updateRows)
        {
            sources.Clear();
            sources.AddRange(outcome.Rows);
            ReconcileSourcePreferences();
            ApplySourcePreferences();
        }
    }

    private IReadOnlyList<string> GetEnabledSourceNames() =>
        sources.Where(source => source.IsEnabled)
            .Select(source => source.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

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
                packageMetadata[resolution.Package] = resolution;
            }
            return resolution;
        }

        var matches = new List<PackageResolution>();
        foreach (var source in enabledSources)
        {
            var resolution = await packageResolver.ResolveAsync(
                    new PackageIdentity(package.Id, source),
                    cancellationToken)
                .ConfigureAwait(false);
            if (resolution.IsResolved)
            {
                matches.Add(resolution);
            }
        }

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
            packageMetadata[match.Package] = match;
        }
        return match;
    }

    public PackageResolution? GetPackageMetadata(PackageIdentity package)
    {
        lock (packageMetadata)
        {
            return packageMetadata.GetValueOrDefault(package);
        }
    }

    private Dictionary<PackageIdentity, PackageResolution> SnapshotPackageMetadata()
    {
        lock (packageMetadata)
        {
            return new Dictionary<PackageIdentity, PackageResolution>(packageMetadata);
        }
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
                    return !packageMetadata.ContainsKey(package);
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
                    lock (packageMetadata)
                    {
                        packageMetadata[package] = resolution;
                        packageMetadata[resolution.Package] = resolution;
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

    private void ReconcileSourcePreferences()
    {
        var currentNames = sources.Select(source => source.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        disabledSources.RemoveWhere(name => !currentNames.Contains(name));
    }

    private void ApplySourcePreferences()
    {
        for (var index = 0; index < sources.Count; index++)
        {
            var source = sources[index];
            sources[index] = source with { IsEnabled = !disabledSources.Contains(source.Name) };
        }
    }

    private async Task EnsureOfficialSourcesConfiguredAsync(CancellationToken cancellationToken)
    {
        var listOutcome = await sourceService.ListSourcesAsync(cancellationToken).ConfigureAwait(false);
        if (!listOutcome.Succeeded)
        {
            return;
        }

        var currentSources = listOutcome.Rows;
        var isOlderOs = capabilities.WindowsBuildNumber.HasValue && capabilities.WindowsBuildNumber.Value < 19041;
        var isOlderWinget = false;
        if (!string.IsNullOrWhiteSpace(capabilities.WingetVersion))
        {
            var verStr = capabilities.WingetVersion.TrimStart('v').Split('-')[0];
            if (Version.TryParse(verStr, out var wingetVer))
            {
                if (wingetVer < new Version(1, 4))
                {
                    isOlderWinget = true;
                }
            }
        }

        var targetWingetUrl = (isOlderOs || isOlderWinget)
            ? "https://winget.azureedge.net/cache"
            : "https://cdn.winget.microsoft.com/cache";

        var targetMsStoreUrl = "https://storeedgefd.dsx.mp.microsoft.com/v9.0";

        // Verify and update "winget" source
        var wingetSource = currentSources.FirstOrDefault(s => string.Equals(s.Name, "winget", StringComparison.OrdinalIgnoreCase));
        if (wingetSource == null)
        {
            await sourceService.AddSourceAsync("winget", targetWingetUrl, cancellationToken).ConfigureAwait(false);
        }
        else if (!string.Equals(wingetSource.Argument.Trim().TrimEnd('/'), targetWingetUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
        {
            var removeOutcome = await sourceService.RemoveSourceAsync("winget", cancellationToken).ConfigureAwait(false);
            if (removeOutcome.Succeeded)
            {
                await sourceService.AddSourceAsync("winget", targetWingetUrl, cancellationToken).ConfigureAwait(false);
            }
        }

        // Verify and update "msstore" source
        var msstoreSource = currentSources.FirstOrDefault(s => string.Equals(s.Name, "msstore", StringComparison.OrdinalIgnoreCase));
        if (msstoreSource == null)
        {
            await sourceService.AddSourceAsync("msstore", targetMsStoreUrl, cancellationToken).ConfigureAwait(false);
        }
        else if (!string.Equals(msstoreSource.Argument.Trim().TrimEnd('/'), targetMsStoreUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
        {
            var removeOutcome = await sourceService.RemoveSourceAsync("msstore", cancellationToken).ConfigureAwait(false);
            if (removeOutcome.Succeeded)
            {
                await sourceService.AddSourceAsync("msstore", targetMsStoreUrl, cancellationToken).ConfigureAwait(false);
            }
        }

        // Ensure default sources are enabled (active)
        var preferencesChanged = false;
        if (disabledSources.Contains("winget"))
        {
            disabledSources.Remove("winget");
            preferencesChanged = true;
        }
        if (disabledSources.Contains("msstore"))
        {
            disabledSources.Remove("msstore");
            preferencesChanged = true;
        }

        if (preferencesChanged || !defaultSourcesConfigured)
        {
            defaultSourcesConfigured = true;
            await sourcePreferences.SaveAsync(
                new SourcePreferences(disabledSources.ToArray(), DefaultSourcesConfigured: true),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static string CreateOperationActivityMessage(OperationExecutionResult result)
    {
        var exitCode = result.CommandResult.ExitCode;
        var exitCodeSuffix = exitCode != 0
            ? $" (Exit code: {exitCode} / 0x{exitCode:X8})"
            : string.Empty;

        if (result.Error is not null)
        {
            var baseMsg = string.IsNullOrWhiteSpace(result.CommandResult.StandardError)
                ? result.Error.Message
                : $"{result.Error.Message} {result.CommandResult.StandardError.Trim()}";
            return baseMsg + exitCodeSuffix;
        }

        var output = result.CommandResult.StandardOutput.Trim();
        if (!string.IsNullOrWhiteSpace(output))
        {
            return output + exitCodeSuffix;
        }

        var errorOutput = result.CommandResult.StandardError.Trim();
        var finalMsg = string.IsNullOrWhiteSpace(errorOutput) ? "Completed." : errorOutput;
        return finalMsg + exitCodeSuffix;
    }

    private async Task<ApplicationActionResult> RunAsync(
        ApplicationBusyState state,
        Func<Task> action,
        string fallbackError)
    {
        if (Interlocked.CompareExchange(ref operationInProgress, 1, 0) != 0)
        {
            return ApplicationActionResult.Failure("Another operation is already in progress.");
        }

        busyState = state;
        userVisibleError = null;
        NotifyStateChanged();
        Logger?.Invoke(AppLogLevel.Verbose, $"Starting operation {state}...", "RunAsync");
        try
        {
            await action().ConfigureAwait(false);
            Logger?.Invoke(AppLogLevel.Verbose, $"Operation {state} completed successfully.", "RunAsync");
            return ApplicationActionResult.Success;
        }
        catch (OperationCanceledException)
        {
            Logger?.Invoke(AppLogLevel.Information, $"Operation {state} was cancelled.", "RunAsync");
            return Fail("Operation cancelled.", state);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            Logger?.Invoke(AppLogLevel.Warning, $"Operation {state} failed with user error: {exception.Message}", "RunAsync");
            return Fail(exception.Message, state);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            ExceptionLogger?.Invoke("OnlyWingetApplication.RunAsync", exception);
            Logger?.Invoke(AppLogLevel.Error, $"Operation {state} failed: {exception}", "RunAsync");
            return Fail(fallbackError, state);
        }
        finally
        {
            busyState = ApplicationBusyState.Idle;
            Interlocked.Exchange(ref operationInProgress, 0);
            NotifyStateChanged();
        }
    }

    private ApplicationActionResult Run(Action action)
    {
        userVisibleError = null;
        try
        {
            action();
            return ApplicationActionResult.Success;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return Fail(exception.Message, ApplicationBusyState.Idle);
        }
        finally
        {
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    private ApplicationActionResult ToggleSelection<TKey>(SelectionState<TKey> selection, TKey key)
        where TKey : notnull =>
        Run(() => selection.Toggle(key));

    public ApplicationActionResult SetPresetPackagesInclusion(IEnumerable<PackageIdentity> packages, bool isSelected) =>
        Run(() => { foreach (var p in packages) presetInstallSelection.SetSelected(p, isSelected); });

    public ApplicationActionResult SetSearchResultsSelection(IEnumerable<PackageIdentity> packages, bool isSelected) =>
        Run(() => { foreach (var p in packages) searchSelection.SetSelected(p, isSelected); });

    public ApplicationActionResult SetUpdatesSelection(IEnumerable<PackageIdentity> packages, bool isSelected) =>
        Run(() => { foreach (var p in packages) updateSelection.SetSelected(p, isSelected); });

    public ApplicationActionResult SetWindowsUpdatesSelection(IEnumerable<WindowsUpdateIdentity> updates, bool isSelected) =>
        Run(() => { foreach (var u in updates) windowsUpdateSelection.SetSelected(u, isSelected); });

    public async Task<ApplicationActionResult> AddPackagesToActivePresetAsync(
        IEnumerable<PackageIdentity> packages,
        CancellationToken cancellationToken) =>
        await RunAsync(ApplicationBusyState.ValidatingPackages, async () =>
        {
            ArgumentNullException.ThrowIfNull(packages);
            var active = RequireActivePreset();
            var updatedPackages = active.Packages.ToList();
            var addedCount = 0;
            var errors = new List<string>();

            foreach (var package in packages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var validated = await ValidatePackageAsync(package, cancellationToken).ConfigureAwait(false);
                    if (updatedPackages.Contains(validated.Package))
                    {
                        continue;
                    }
                    updatedPackages.Add(validated.Package);
                    addedCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"{package.Id}: {ex.Message}");
                }
            }

            if (addedCount > 0)
            {
                ReplacePreset(active.Name, new Preset(active.Name, updatedPackages), active.Name);
                AddActivity(ActivitySeverity.Success, "Packages pasted", $"{addedCount} package(s) added.");
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException($"Failed to add some packages:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
            }
        }, "Unable to validate pasted packages.").ConfigureAwait(false);

    private void RequireWinget()
    {
        if (!capabilities.CanUseWinget)
        {
            throw new NotSupportedException(capabilities.WingetUnavailableMessage);
        }
    }

    private void RequireWindowsUpdate()
    {
        if (!capabilities.CanUseWindowsUpdate)
        {
            throw new NotSupportedException(capabilities.WindowsUpdateUnavailableMessage);
        }
    }

    private ApplicationActionResult Fail(string error, ApplicationBusyState state = ApplicationBusyState.Idle)
    {
        if (state != ApplicationBusyState.ExecutingOperation)
        {
            userVisibleError = error;
        }
        AddActivity(ActivitySeverity.Error, "Action failed", error);
        return ApplicationActionResult.Failure(error);
    }

    private void AddActivity(ActivitySeverity severity, string title, string message)
    {
        activity.Add(new ActivityEntry(clock.GetUtcNow(), severity, title, message));
        var logLevel = severity switch
        {
            ActivitySeverity.Error => AppLogLevel.Error,
            ActivitySeverity.Warning => AppLogLevel.Warning,
            _ => AppLogLevel.Information
        };
        Logger?.Invoke(logLevel, $"[Activity] {title}: {message}", nameof(AddActivity));
    }

    private Preset? ActivePreset =>
        workspace.ActivePresetName is null
            ? workspace.Presets.FirstOrDefault()
            : FindPreset(workspace.ActivePresetName) ?? workspace.Presets.FirstOrDefault();

    private Preset RequireActivePreset() =>
        ActivePreset ?? throw new InvalidOperationException("Create or select a preset first.");

    private Preset EnsureActivePreset()
    {
        if (ActivePreset is { } active)
        {
            return active;
        }

        var preset = new Preset("Default", []);
        workspace = NormalizeWorkspace(new WorkspaceState([preset], preset.Name));
        RefreshPresetSelection();
        AddActivity(ActivitySeverity.Information, "Preset created", preset.Name);
        return preset;
    }

    private Preset? FindPreset(string name) =>
        workspace.Presets.FirstOrDefault(preset => PresetNameEquals(preset.Name, name));

    private void ReplacePreset(string oldName, Preset replacement, string activeName)
    {
        var presets = workspace.Presets
            .Select(preset => PresetNameEquals(preset.Name, oldName) ? replacement : preset)
            .ToArray();
        workspace = NormalizeWorkspace(new WorkspaceState(presets, activeName));
        RefreshPresetSelection();
    }

    private void RefreshPresetSelection()
    {
        var packages = ActivePreset?.Packages ?? [];
        presetSelection.ReplaceAvailable(packages);
        presetInstallSelection.ReplaceAvailable(packages, selectAvailable: true);
    }

    private static WorkspaceState NormalizeWorkspace(WorkspaceState state)
    {
        var presets = state.Presets
            .GroupBy(preset => preset.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var preset = group.First();
                return new Preset(
                    preset.Name,
                    preset.Packages
                        .OrderBy(package => package.Id, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(package => package.Source, StringComparer.OrdinalIgnoreCase)
                        .ToArray());
            })
            .OrderBy(preset => preset.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var activeName = state.ActivePresetName is not null &&
            presets.Any(preset => PresetNameEquals(preset.Name, state.ActivePresetName))
                ? presets.First(preset => PresetNameEquals(preset.Name, state.ActivePresetName)).Name
                : presets.FirstOrDefault()?.Name;

        return new WorkspaceState(presets, activeName);
    }

    private static bool IsUpToDate(string? installed, string? available)
    {
        if (string.IsNullOrWhiteSpace(installed)) return false;
        if (string.IsNullOrWhiteSpace(available)) return true;

        installed = installed.Trim();
        available = available.Trim();

        if (string.Equals(installed, available, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Version.TryParse(installed, out var installedVer) && Version.TryParse(available, out var availableVer))
        {
            return installedVer >= availableVer;
        }

        var installedParts = installed.Split('.', '-', '+', '_');
        var availableParts = available.Split('.', '-', '+', '_');

        for (int i = 0; i < Math.Min(installedParts.Length, availableParts.Length); i++)
        {
            var instPart = installedParts[i];
            var availPart = availableParts[i];

            if (int.TryParse(instPart, out var instInt) && int.TryParse(availPart, out var availInt))
            {
                if (instInt != availInt)
                {
                    return instInt > availInt;
                }
            }
            else
            {
                var cmp = string.Compare(instPart, availPart, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0)
                {
                    return cmp > 0;
                }
            }
        }

        return installedParts.Length >= availableParts.Length;
    }

    private static string WindowsUpdateFingerprint(WindowsUpdateIdentity update) =>
        $"{update.UpdateId.ToUpperInvariant()}|{update.RevisionNumber}";

    private static bool PresetNameEquals(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private sealed class EmptySourcePreferenceStore : ISourcePreferenceStore
    {
        public Task<SourcePreferences> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new SourcePreferences([], DefaultSourcesConfigured: true));

        public Task SaveAsync(SourcePreferences preferences, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
