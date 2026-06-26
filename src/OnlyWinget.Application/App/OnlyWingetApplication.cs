using OnlyWinget.Application.Activity;
using OnlyWinget.Application.Operations;
using OnlyWinget.Application.Presets;
using OnlyWinget.Application.Storage;
using OnlyWinget.Application.Winget;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Domain.Operations;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Presets;
using OnlyWinget.Domain.Selection;

namespace OnlyWinget.Application.App;

public sealed class OnlyWingetApplication(
    IWorkspaceStore workspaceStore,
    ICommandAvailability commandAvailability,
    IPackageSearchService packageSearch,
    IPackageResolver packageResolver,
    IUpdateLoader updateLoader,
    IWindowsUpdateService windowsUpdateService,
    IWingetSourceService sourceService,
    IOperationExecutor operationExecutor,
    TimeProvider? timeProvider = null)
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
    private readonly List<WindowsUpdateInstallResult> lastWindowsUpdateResults = [];
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    private WorkspaceState workspace = WorkspaceState.Empty;
    private ApplicationBusyState busyState;
    private bool? isWingetAvailable;
    private ClassifiedWingetError? sourceError;
    private string? userVisibleError;

    public OnlyWingetState State => CreateState();

    public async Task<ApplicationActionResult> LoadWorkspaceAsync(CancellationToken cancellationToken)
    {
        return await RunAsync(
                ApplicationBusyState.LoadingWorkspace,
                async () =>
                {
                    workspace = NormalizeWorkspace(await workspaceStore.LoadAsync(cancellationToken).ConfigureAwait(false));
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

    public async Task<ApplicationActionResult> CheckWingetAsync(CancellationToken cancellationToken)
    {
        return await RunAsync(
                ApplicationBusyState.CheckingWinget,
                async () =>
                {
                    isWingetAvailable = await commandAvailability.IsWingetAvailableAsync(cancellationToken)
                        .ConfigureAwait(false);
                    AddActivity(
                        isWingetAvailable.Value ? ActivitySeverity.Success : ActivitySeverity.Error,
                        "winget checked",
                        isWingetAvailable.Value ? "winget is available." : "winget is not available on PATH.");
                },
                "Unable to check winget availability.")
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

    public ApplicationActionResult AddPackageToActivePreset(PackageIdentity package) =>
        Run(() =>
        {
            ArgumentNullException.ThrowIfNull(package);
            var active = RequireActivePreset();
            if (ContainsPackage(active.Packages, package))
            {
                throw new InvalidOperationException("Package is already in the active preset.");
            }

            ReplacePreset(active.Name, new Preset(active.Name, [.. active.Packages, package]), active.Name);
            AddActivity(ActivitySeverity.Success, "Package added", package.Id);
        });

    public ApplicationActionResult ReplacePackageInActivePreset(PackageIdentity current, PackageIdentity replacement) =>
        Run(() =>
        {
            ArgumentNullException.ThrowIfNull(current);
            ArgumentNullException.ThrowIfNull(replacement);
            var active = RequireActivePreset();
            if (!ContainsPackage(active.Packages, current))
            {
                throw new InvalidOperationException("Package was not found in the active preset.");
            }

            if (!PackageEquals(current, replacement) && ContainsPackage(active.Packages, replacement))
            {
                throw new InvalidOperationException("Package is already in the active preset.");
            }

            var packages = active.Packages
                .Select(package => PackageEquals(package, current) ? replacement : package)
                .ToArray();
            ReplacePreset(active.Name, new Preset(active.Name, packages), active.Name);
            AddActivity(ActivitySeverity.Success, "Package updated", replacement.Id);
        });

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
            AddActivity(ActivitySeverity.Success, "Packages removed", selected.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        });

    public ApplicationActionResult TogglePresetPackage(PackageIdentity package) => ToggleSelection(presetSelection, package);

    public ApplicationActionResult ToggleAllPresetPackages() => Run(presetSelection.ToggleAll);

    public async Task<ApplicationActionResult> SearchAsync(string query, string? source, CancellationToken cancellationToken)
    {
        return await RunAsync(
                ApplicationBusyState.Searching,
                async () =>
                {
                    var outcome = await packageSearch.SearchAsync(new PackageSearchRequest(query, source), cancellationToken)
                        .ConfigureAwait(false);
                    if (!outcome.Succeeded)
                    {
                        throw new InvalidOperationException(outcome.Error?.Message ?? "winget search failed.");
                    }

                    searchResults.Clear();
                    searchResults.AddRange(outcome.Rows.DistinctBy(result => PackageFingerprint(result.Package)));
                    searchSelection.ReplaceAvailable(searchResults.Select(result => result.Package));
                    AddActivity(ActivitySeverity.Information, "Search completed", $"{searchResults.Count} result(s).");
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
                    var active = EnsureActivePreset();
                    var packages = active.Packages.ToList();
                    var added = 0;
                    foreach (var selected in searchSelection.Selected)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var resolution = await packageResolver.ResolveAsync(selected, cancellationToken).ConfigureAwait(false);
                        var package = resolution.IsResolved ? resolution.Package : selected;
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
                    var outcome = await updateLoader.LoadUpdatesAsync(cancellationToken).ConfigureAwait(false);
                    if (!outcome.Succeeded && outcome.Error?.Kind != WingetErrorKind.NoUpdates)
                    {
                        throw new InvalidOperationException(outcome.Error?.Message ?? "winget upgrade failed.");
                    }

                    updates.Clear();
                    updates.AddRange(outcome.Rows.DistinctBy(update => PackageFingerprint(update.Package)));
                    updateSelection.ReplaceAvailable(updates.Select(update => update.Package));
                    AddActivity(ActivitySeverity.Information, "Updates refreshed", $"{updates.Count} update(s).");
                },
                "Unable to refresh updates.")
            .ConfigureAwait(false);
    }

    public async Task<ApplicationActionResult> ScanWindowsUpdatesAsync(CancellationToken cancellationToken)
    {
        return await RunAsync(
                ApplicationBusyState.ScanningWindowsUpdates,
                async () =>
                {
                    var outcome = await windowsUpdateService.ScanAsync(cancellationToken).ConfigureAwait(false);
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

    public async Task<ApplicationActionResult> InstallSelectedWindowsUpdatesAsync(CancellationToken cancellationToken)
    {
        var selected = windowsUpdateSelection.Selected.ToArray();
        return await RunAsync(
                ApplicationBusyState.InstallingWindowsUpdates,
                async () =>
                {
                    if (selected.Length == 0)
                    {
                        throw new InvalidOperationException("Select at least one Windows update before installing.");
                    }

                    lastWindowsUpdateResults.Clear();
                    AddActivity(ActivitySeverity.Information, "Windows Update install started", $"{selected.Length} update(s).");
                    var outcome = await windowsUpdateService.InstallAsync(selected, cancellationToken).ConfigureAwait(false);
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
                    var outcome = await sourceService.ListSourcesAsync(cancellationToken).ConfigureAwait(false);
                    ApplySourceOutcome(outcome, updateRows: true);
                    AddActivity(ActivitySeverity.Information, "Sources refreshed", $"{sources.Count} source(s).");
                },
                "Unable to refresh winget sources.")
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
        return await RunSourceMutationAsync(
                () => sourceService.ResetSourcesAsync(cancellationToken),
                "Sources reset",
                "winget sources reset to defaults.",
                cancellationToken)
            .ConfigureAwait(false);
    }

    public ApplicationActionResult ToggleUpdate(PackageIdentity package) => ToggleSelection(updateSelection, package);

    public ApplicationActionResult ToggleAllUpdates() => Run(updateSelection.ToggleAll);

    public async Task<ApplicationActionResult> ApplySelectedUpdatesAsync(CancellationToken cancellationToken)
    {
        var selections = updateSelection.Selected
            .Select(package => new PackageSelection(package, PackageAction.Upgrade))
            .ToArray();
        return await ExecutePlanAsync(new OperationPlan("Selected updates", selections), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ApplicationActionResult> ApplyActivePresetAsync(PackageAction action, CancellationToken cancellationToken)
    {
        var plan = operationPlanner.CreatePresetPlan(RequireActivePreset(), action);
        return await ExecutePlanAsync(plan, cancellationToken).ConfigureAwait(false);
    }

    public ApplicationActionResult ClearActivity() =>
        Run(() =>
        {
            activity.Clear();
            userVisibleError = null;
        });

    public string ExportActivePreset()
    {
        var active = RequireActivePreset();
        return presetDocuments.Export(active);
    }

    public ApplicationActionResult ImportPreset(string json) =>
        Run(() =>
        {
            var preset = presetDocuments.Import(json);
            if (FindPreset(preset.Name) is not null)
            {
                throw new InvalidOperationException("A preset with the same name already exists.");
            }

            workspace = new WorkspaceState([.. workspace.Presets, preset], preset.Name);
            RefreshPresetSelection();
            AddActivity(ActivitySeverity.Success, "Preset imported", preset.Name);
        });

    private async Task<ApplicationActionResult> ExecutePlanAsync(OperationPlan plan, CancellationToken cancellationToken)
    {
        return await RunAsync(
                ApplicationBusyState.ExecutingOperation,
                async () =>
                {
                    if (!plan.HasWork)
                    {
                        throw new InvalidOperationException("Select at least one package before applying an operation.");
                    }

                    lastOperationResults.Clear();
                    AddActivity(ActivitySeverity.Information, "Operation started", plan.Name);
                    var summary = await operationExecutor.ExecuteAsync(plan, cancellationToken).ConfigureAwait(false);
                    lastOperationResults.AddRange(summary.Results);
                    foreach (var result in summary.Results)
                    {
                        var severity = result.Succeeded ? ActivitySeverity.Success : ActivitySeverity.Error;
                        var message = CreateOperationActivityMessage(result);
                        AddActivity(severity, result.Selection.Package.Id, string.IsNullOrWhiteSpace(message) ? "Completed." : message);
                    }

                    if (!summary.Succeeded)
                    {
                        throw new InvalidOperationException("One or more winget operations failed.");
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
            isWingetAvailable,
            sources.ToArray(),
            sourceError,
            activity.ToArray(),
            lastOperationResults.ToArray(),
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
                    var outcome = await operation().ConfigureAwait(false);
                    ApplySourceOutcome(outcome, updateRows: false);
                    AddActivity(ActivitySeverity.Success, title, message);

                    var refresh = await sourceService.ListSourcesAsync(cancellationToken).ConfigureAwait(false);
                    ApplySourceOutcome(refresh, updateRows: true);
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
        busyState = state;
        userVisibleError = null;
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
    }

    private ApplicationActionResult ToggleSelection<TKey>(SelectionState<TKey> selection, TKey key)
        where TKey : notnull =>
        Run(() => selection.Toggle(key));

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
}
