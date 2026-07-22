using OnlyWinget.Application.Activity;
using OnlyWinget.Application.Storage;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Presets;
using OnlyWinget.Domain.Selection;

namespace OnlyWinget.Application.App;

public sealed partial class OnlyWingetApplication
{
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

    public ApplicationActionResult SetPresetPackagesInclusion(IEnumerable<PackageIdentity> packages, bool isSelected) =>
        Run(() => { foreach (var p in packages) presetInstallSelection.SetSelected(p, isSelected); });

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
}
