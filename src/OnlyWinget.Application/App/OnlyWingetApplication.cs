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
    private readonly PresetDocumentService presetDocuments = new();
    private readonly OperationPlanner operationPlanner = new();
    private readonly SelectionState<PackageIdentity> presetSelection = new();
    private readonly SelectionState<PackageIdentity> searchSelection = new();
    private readonly SelectionState<PackageIdentity> updateSelection = new();
    private readonly SelectionState<WindowsUpdateIdentity> windowsUpdateSelection = new();
    private readonly List<PackageSearchResult> searchResults = [];
    private readonly List<PackageUpdate> updates = [];
    private readonly List<WindowsUpdateItem> windowsUpdates = [];
    private readonly List<WingetSource> sources = [];
    private readonly List<ActivityEntry> activity = [];
    private readonly List<OperationExecutionResult> lastOperationResults = [];
    private readonly Dictionary<string, PackageResolution> packageMetadata = new(StringComparer.Ordinal);
    private readonly List<WindowsUpdateInstallResult> lastWindowsUpdateResults = [];
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly ISourcePreferenceStore sourcePreferences = sourcePreferenceStore ?? new EmptySourcePreferenceStore();
    private readonly HashSet<string> disabledSources = new(StringComparer.OrdinalIgnoreCase);

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

            workspace = new WorkspaceState([.. workspace.Presets, preset], preset.Name);
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
            if (ContainsPackage(active.Packages, validated.Package))
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
            if (!ContainsPackage(active.Packages, current))
            {
                throw new InvalidOperationException("Package was not found in the active preset.");
            }

            if (!PackageEquals(current, validated.Package) && ContainsPackage(active.Packages, validated.Package))
            {
                throw new InvalidOperationException("Package is already in the active preset.");
            }

            var packages = active.Packages
                .Select(package => PackageEquals(package, current) ? validated.Package : package)
                .ToArray();
            ReplacePreset(active.Name, new Preset(active.Name, packages), active.Name);
            AddActivity(ActivitySeverity.Success, "Package updated", validated.Package.Id);
        }, "Unable to validate the package.").ConfigureAwait(false);

    public ApplicationActionResult RemoveSelectedPackagesFromActivePreset() =>
        Run(() =>
        {
            var active = RequireActivePreset();
            var selected = presetSelection.Selected.ToArray();
            if (selected.Length == 0)
            {
                throw new InvalidOperationException("Select at least one package to remove.");
            }

            var packages = active.Packages
                .Where(package => !selected.Any(selectedPackage => PackageEquals(selectedPackage, package)))
                .ToArray();
            ReplacePreset(active.Name, new Preset(active.Name, packages), active.Name);
            AddActivity(ActivitySeverity.Success, "Packages removed", selected.Length.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
        });

    public ApplicationActionResult TogglePresetPackage(PackageIdentity package) => ToggleSelection(presetSelection, package);

    public ApplicationActionResult ToggleAllPresetPackages() => Run(presetSelection.ToggleAll);

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
                    foreach (var source in enabledSources)
                    {
                        var outcome = await packageSearch.SearchAsync(new PackageSearchRequest(query, source), cancellationToken)
                            .ConfigureAwait(false);
                        if (!outcome.Succeeded)
                        {
                            sourceErrors.Add($"{source}: {outcome.Error?.Message ?? "winget search failed."}");
                            continue;
                        }

                        searchResults.AddRange(outcome.Rows);
                    }

                    if (searchResults.Count == 0 && sourceErrors.Count > 0)
                    {
                        throw new InvalidOperationException(string.Join(Environment.NewLine, sourceErrors));
                    }

                    var distinctResults = searchResults
                        .DistinctBy(result => PackageFingerprint(result.Package))
                        .OrderBy(result => result.Name, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(result => result.Package.Id, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    searchResults.Clear();
                    searchResults.AddRange(distinctResults);
                    searchSelection.ReplaceAvailable(searchResults.Select(result => result.Package));
                    AddActivity(ActivitySeverity.Information, "Search completed", $"{searchResults.Count} result(s).");
                    if (sourceErrors.Count > 0)
                    {
                        AddActivity(
                            ActivitySeverity.Warning,
                            "Some sources could not be searched",
                            string.Join(Environment.NewLine, sourceErrors));
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
                        if (ContainsPackage(packages, package))
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
                    var sourceErrors = new List<string>();
                    foreach (var source in enabledSources)
                    {
                        var outcome = await updateLoader.LoadUpdatesAsync(source, cancellationToken).ConfigureAwait(false);
                        if (!outcome.Succeeded && outcome.Error?.Kind != WingetErrorKind.NoUpdates)
                        {
                            sourceErrors.Add($"{source}: {outcome.Error?.Message ?? "winget upgrade failed."}");
                            continue;
                        }

                        updates.AddRange(outcome.Rows);
                    }

                    if (updates.Count == 0 && sourceErrors.Count > 0)
                    {
                        throw new InvalidOperationException(string.Join(Environment.NewLine, sourceErrors));
                    }

                    var distinctUpdates = updates
                        .DistinctBy(update => PackageFingerprint(update.Package))
                        .OrderBy(update => update.Name, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    updates.Clear();
                    updates.AddRange(distinctUpdates);
                    foreach (var update in updates)
                    {
                        var resolution = await packageResolver.ResolveAsync(update.Package, cancellationToken).ConfigureAwait(false);
                        if (resolution.IsResolved)
                        {
                            packageMetadata[PackageFingerprint(update.Package)] = resolution;
                        }
                    }

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
                    var outcome = await windowsUpdateService.ScanAsync(options, cancellationToken).ConfigureAwait(false);
                    if (!outcome.Succeeded)
                    {
                        throw new InvalidOperationException(outcome.Error?.Message ?? "Windows Update scan failed.");
                    }

                    windowsUpdates.Clear();
                    windowsUpdates.AddRange(outcome.Rows.DistinctBy(update => WindowsUpdateFingerprint(update.Identity)));
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
                        AddActivity(
                            result.Succeeded ? ActivitySeverity.Success : ActivitySeverity.Error,
                            result.Title,
                            string.IsNullOrWhiteSpace(result.Message) ? result.ResultCode : result.Message);
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
                        .DistinctBy(PackageFingerprint)
                        .ToArray();
                    foreach (var package in packages)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var resolution = await packageResolver.ResolveAsync(package, cancellationToken).ConfigureAwait(false);
                        if (resolution.IsResolved)
                        {
                            packageMetadata[PackageFingerprint(package)] = resolution;
                        }
                    }

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
            ApplySourcePreferences();
            await sourcePreferences.SaveAsync(SourcePreferences.Empty, cancellationToken).ConfigureAwait(false);
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
        var plan = operationPlanner.CreatePresetPlan(RequireActivePreset(), action);
        return await ExecutePlanAsync(plan, cancellationToken, progress).ConfigureAwait(false);
    }

    public ApplicationActionResult ClearActivity() =>
        Run(() =>
        {
            activity.Clear();
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
            workspace = new WorkspaceState([.. workspace.Presets, validatedPreset], validatedPreset.Name);
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
                    foreach (var selection in plan.Selections)
                    {
                        var validated = await ValidatePackageAsync(selection.Package, cancellationToken).ConfigureAwait(false);
                        validatedSelections.Add(new PackageSelection(validated.Package, selection.Action));
                    }

                    var validatedPlan = new OperationPlan(plan.Name, validatedSelections);
                    lastOperationResults.Clear();
                    AddActivity(ActivitySeverity.Information, "Operation started", plan.Name);
                    operationProgress = new OperationProgress(string.Empty, WingetProgressPhase.Starting, 0, 0, validatedPlan.Selections.Count);
                    var forwardingProgress = new InlineProgress<OperationProgress>(update =>
                    {
                        operationProgress = update;
                        progress?.Report(update);
                        NotifyStateChanged();
                    });
                    var summary = await operationExecutor.ExecuteAsync(validatedPlan, cancellationToken, forwardingProgress).ConfigureAwait(false);
                    lastOperationResults.AddRange(summary.Results);
                    foreach (var result in summary.Results)
                    {
                        var severity = result.Succeeded ? ActivitySeverity.Success : ActivitySeverity.Error;
                        var message = CreateOperationActivityMessage(result);
                        AddActivity(severity, result.Selection.Package.Id, string.IsNullOrWhiteSpace(message) ? "Completed." : message);
                    }

                    var succeededPackages = summary.Results
                        .Where(result => result.Succeeded)
                        .Select(result => result.Selection.Package)
                        .ToArray();
                    updates.RemoveAll(update => succeededPackages.Any(package => PackageEquals(package, update.Package)));
                    updateSelection.ReplaceAvailable(updates.Select(update => update.Package));

                    if (!summary.Succeeded)
                    {
                        throw new InvalidOperationException("One or more winget operations failed.");
                    }

                    operationProgress = operationProgress with { Phase = WingetProgressPhase.Completed, Percentage = 100, CompletedPackages = validatedPlan.Selections.Count };
                    progress?.Report(operationProgress);
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
            presetSelection.Selected.ToArray(),
            presetSelection.HeaderState,
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
            new Dictionary<string, PackageResolution>(packageMetadata, StringComparer.Ordinal),
            capabilities,
            sources.ToArray(),
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
                            new SourcePreferences(disabledSources.ToArray()),
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

            packageMetadata[PackageFingerprint(resolution.Package)] = resolution;
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
        packageMetadata[PackageFingerprint(match.Package)] = match;
        return match;
    }

    public PackageResolution? GetPackageMetadata(PackageIdentity package) =>
        packageMetadata.GetValueOrDefault(PackageFingerprint(package));

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

    private static string CreateOperationActivityMessage(OperationExecutionResult result)
    {
        if (result.Error is not null)
        {
            return string.IsNullOrWhiteSpace(result.CommandResult.StandardError)
                ? result.Error.Message
                : $"{result.Error.Message} {result.CommandResult.StandardError.Trim()}";
        }

        var output = result.CommandResult.StandardOutput.Trim();
        if (!string.IsNullOrWhiteSpace(output))
        {
            return output;
        }

        var errorOutput = result.CommandResult.StandardError.Trim();
        return string.IsNullOrWhiteSpace(errorOutput) ? "Completed." : errorOutput;
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
        try
        {
            await action().ConfigureAwait(false);
            return ApplicationActionResult.Success;
        }
        catch (OperationCanceledException)
        {
            return Fail("Operation cancelled.");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return Fail(exception.Message);
        }
        catch (Exception)
        {
            return Fail(fallbackError);
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
            return Fail(exception.Message);
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

    private ApplicationActionResult Fail(string error)
    {
        userVisibleError = error;
        AddActivity(ActivitySeverity.Error, "Action failed", error);
        return ApplicationActionResult.Failure(error);
    }

    private void AddActivity(ActivitySeverity severity, string title, string message) =>
        activity.Add(new ActivityEntry(clock.GetUtcNow(), severity, title, message));

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
        presetSelection.ReplaceAvailable(ActivePreset?.Packages ?? []);
    }

    private static WorkspaceState NormalizeWorkspace(WorkspaceState state)
    {
        var presets = state.Presets
            .GroupBy(preset => preset.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        var activeName = state.ActivePresetName is not null &&
            presets.Any(preset => PresetNameEquals(preset.Name, state.ActivePresetName))
                ? presets.First(preset => PresetNameEquals(preset.Name, state.ActivePresetName)).Name
                : presets.FirstOrDefault()?.Name;

        return new WorkspaceState(presets, activeName);
    }

    private static bool ContainsPackage(IEnumerable<PackageIdentity> packages, PackageIdentity package) =>
        packages.Any(existing => PackageEquals(existing, package));

    private static bool PackageEquals(PackageIdentity left, PackageIdentity right) =>
        string.Equals(left.Id, right.Id, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.Source ?? string.Empty, right.Source ?? string.Empty, StringComparison.OrdinalIgnoreCase);

    private static string PackageFingerprint(PackageIdentity package) =>
        $"{package.Source?.ToUpperInvariant() ?? string.Empty}|{package.Id.ToUpperInvariant()}";

    private static string WindowsUpdateFingerprint(WindowsUpdateIdentity update) =>
        $"{update.UpdateId.ToUpperInvariant()}|{update.RevisionNumber}";

    private static bool PresetNameEquals(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private sealed class EmptySourcePreferenceStore : ISourcePreferenceStore
    {
        public Task<SourcePreferences> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(SourcePreferences.Empty);

        public Task SaveAsync(SourcePreferences preferences, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
