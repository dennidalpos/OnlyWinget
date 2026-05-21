// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using OnlyWinget.Models;
using OnlyWinget.Services;

namespace OnlyWinget.ViewModels;

public sealed class PackageInterrogationDialogViewModel : ObservableObject
{
    private readonly LocalizedStrings _strings;
    private readonly List<ResolvedInstallerOption> _installerOptions = new();
    private string _packageName = string.Empty;
    private string _packageId = string.Empty;
    private string _version = string.Empty;
    private string _source = string.Empty;
    private string _installerType = string.Empty;
    private string _warningsText = string.Empty;
    private bool _isLoading = true;
    private bool _isReducedMode;
    private bool _isEditMode;
    private string _selectedScope = string.Empty;
    private string _selectedArchitecture = string.Empty;
    private string _selectedLocale = string.Empty;
    private string _selectedInstallerType = string.Empty;
    private string _selectedInstallMode = InstallModes.SilentWithProgress;
    private string _installLocation = string.Empty;
    private string _selectedInstallLocationPreset = string.Empty;
    private string _logPath = string.Empty;
    private string _additionalCustomArgs = string.Empty;
    private string _overrideArgs = string.Empty;
    private bool _isLocationSupported = true;
    private bool _isLogSupported = true;
    private string _elevationRequirement = string.Empty;
    private string _unsupportedArgumentsText = string.Empty;
    private string _commandPreview = string.Empty;
    private bool _isUpdatingArchitectureSelections;
    private bool _isApplyingInstallLocationPreset;

    public PackageInterrogationDialogViewModel(LocalizedStrings strings)
    {
        _strings = strings;
        AvailableScopes = new ObservableCollection<string>();
        AvailableArchitectures = new ObservableCollection<string>();
        AvailableArchitectureOptions = new ObservableCollection<SelectableOption>();
        AvailableLocales = new ObservableCollection<string>();
        AvailableInstallerTypes = new ObservableCollection<string>();
        AvailableInstallModes = new ObservableCollection<string>();
        InstallLocationPresets = new ObservableCollection<string>();
    }

    public string Title => _strings.PackageDialogTitle;
    public string ConfirmLabel => _strings.PackageDialogConfirmLabel;
    public string CancelLabel => _strings.PackageDialogCancelLabel;
    public string LoadingText => _strings.PackageDialogLoadingText;
    public string ReducedModeText => _strings.PackageDialogReducedModeText;
    public string WarningsTitle => _strings.PackageDialogWarningsTitle;
    public string SourceLabel => _strings.PackageDialogSourceLabel;
    public string InstallerTypeLabel => _strings.PackageDialogInstallerTypeLabel;
    public string DetectedInstallerTypeLabel => _strings.PackageDialogDetectedInstallerTypeLabel;
    public string ScopeLabel => _strings.PackageDialogScopeLabel;
    public string InstallModeLabel => _strings.PackageDialogInstallModeLabel;
    public string ArchitectureLabel => _strings.PackageDialogArchitectureLabel;
    public string LocaleLabel => _strings.PackageDialogLocaleLabel;
    public string InstallLocationLabel => _strings.PackageDialogInstallLocationLabel;
    public string InstallLocationPresetLabel => _strings.PackageDialogInstallLocationPresetLabel;
    public string LogPathLabel => _strings.PackageDialogLogPathLabel;
    public string AdditionalCustomArgsLabel => _strings.PackageDialogAdditionalCustomArgsLabel;
    public string OverrideArgsLabel => _strings.PackageDialogOverrideArgsLabel;
    public string AdvancedTitle => _strings.PackageDialogAdvancedTitle;
    public string AdvancedArgumentsWarningText => _strings.PackageDialogAdvancedArgumentsWarningText;
    public string CommandPreviewLabel => _strings.PackageDialogCommandPreviewLabel;
    public string OverrideWarningText => _strings.PackageDialogOverrideWarningText;
    public string LocationUnsupportedText => _strings.PackageDialogLocationUnsupportedText;
    public string LogUnsupportedText => _strings.PackageDialogLogUnsupportedText;
    public string ElevationRequirementLabel => _strings.PackageDialogElevationRequirementLabel;
    public string UnsupportedArgumentsLabel => _strings.PackageDialogUnsupportedArgumentsLabel;
    public string PackageNameLabel => _strings.PackageDialogPackageNameLabel;
    public string PackageIdLabel => _strings.PackageDialogPackageIdLabel;
    public string PackageVersionLabel => _strings.PackageDialogPackageVersionLabel;

    public string PackageName
    {
        get => _packageName;
        private set => SetProperty(ref _packageName, value);
    }

    public string PackageId
    {
        get => _packageId;
        private set => SetProperty(ref _packageId, value);
    }

    public string Version
    {
        get => _version;
        private set => SetProperty(ref _version, value);
    }

    public string Source
    {
        get => _source;
        private set => SetProperty(ref _source, value);
    }

    public string InstallerType
    {
        get => _installerType;
        private set => SetProperty(ref _installerType, value);
    }

    public string WarningsText
    {
        get => _warningsText;
        private set
        {
            if (SetProperty(ref _warningsText, value))
            {
                OnPropertyChanged(nameof(HasWarnings));
            }
        }
    }

    public bool HasWarnings => !string.IsNullOrWhiteSpace(WarningsText);

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(CanConfirm));
                OnPropertyChanged(nameof(IsContentReady));
                OnPropertyChanged(nameof(IsFullModeContentReady));
            }
        }
    }

    public bool CanConfirm => !IsLoading && HasValidArchitectureSelection && HasMatchingInstallerSelection;

    public bool IsContentReady => !IsLoading;

    public bool IsReducedMode
    {
        get => _isReducedMode;
        private set
        {
            if (SetProperty(ref _isReducedMode, value))
            {
                OnPropertyChanged(nameof(IsFullMode));
                OnPropertyChanged(nameof(IsFullModeContentReady));
            }
        }
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        private set
        {
            if (SetProperty(ref _isEditMode, value))
            {
                OnPropertyChanged(nameof(IsArchitectureMultiSelectVisible));
                OnPropertyChanged(nameof(IsArchitectureSingleSelectVisible));
                OnPropertyChanged(nameof(CanConfirm));
                RefreshCommandPreview();
            }
        }
    }

    public bool IsFullMode => !IsReducedMode;

    public bool IsFullModeContentReady => IsContentReady && IsFullMode;

    public ObservableCollection<string> AvailableScopes { get; }
    public ObservableCollection<string> AvailableArchitectures { get; }
    public ObservableCollection<SelectableOption> AvailableArchitectureOptions { get; }
    public ObservableCollection<string> AvailableLocales { get; }
    public ObservableCollection<string> AvailableInstallerTypes { get; }
    public ObservableCollection<string> AvailableInstallModes { get; }
    public ObservableCollection<string> InstallLocationPresets { get; }

    public bool HasScopes => AvailableScopes.Count > 0;
    public bool HasArchitectures => AvailableArchitectures.Count > 0;
    public bool HasLocales => AvailableLocales.Count > 0;
    public bool HasInstallerTypes => AvailableInstallerTypes.Count > 0;
    public bool HasInstallModes => AvailableInstallModes.Count > 0;
    public bool IsArchitectureMultiSelectVisible => HasArchitectures && !IsEditMode && AvailableArchitectures.Count > 1;
    public bool IsArchitectureSingleSelectVisible => HasArchitectures && !IsArchitectureMultiSelectVisible;

    public bool IsLocationSupported
    {
        get => _isLocationSupported;
        private set
        {
            if (SetProperty(ref _isLocationSupported, value))
            {
                OnPropertyChanged(nameof(IsLocationNotSupported));
            }
        }
    }

    public bool IsLocationNotSupported => !IsLocationSupported;

    public bool IsLogSupported
    {
        get => _isLogSupported;
        private set
        {
            if (SetProperty(ref _isLogSupported, value))
            {
                OnPropertyChanged(nameof(IsLogNotSupported));
            }
        }
    }

    public bool IsLogNotSupported => !IsLogSupported;

    public string ElevationRequirement
    {
        get => _elevationRequirement;
        private set
        {
            if (SetProperty(ref _elevationRequirement, value))
            {
                OnPropertyChanged(nameof(HasElevationRequirement));
            }
        }
    }

    public bool HasElevationRequirement => !string.IsNullOrWhiteSpace(ElevationRequirement);

    public string UnsupportedArgumentsText
    {
        get => _unsupportedArgumentsText;
        private set
        {
            if (SetProperty(ref _unsupportedArgumentsText, value))
            {
                OnPropertyChanged(nameof(HasUnsupportedArguments));
            }
        }
    }

    public bool HasUnsupportedArguments => !string.IsNullOrWhiteSpace(UnsupportedArgumentsText);

    public bool ShowOverrideWarning => !string.IsNullOrWhiteSpace(OverrideArgs);

    public bool ShowAdvancedArgumentsWarning =>
        !string.IsNullOrWhiteSpace(AdditionalCustomArgs)
        || !string.IsNullOrWhiteSpace(OverrideArgs);

    public string CommandPreview
    {
        get => _commandPreview;
        private set => SetProperty(ref _commandPreview, value);
    }

    public string SelectedScope
    {
        get => _selectedScope;
        set
        {
            if (SetProperty(ref _selectedScope, value))
            {
                RefreshInstallModes();
            }
        }
    }

    public string SelectedArchitecture
    {
        get => _selectedArchitecture;
        set
        {
            if (SetProperty(ref _selectedArchitecture, value))
            {
                SyncArchitectureSelections(value);
                RefreshInstallModes();
            }
        }
    }

    public string SelectedLocale
    {
        get => _selectedLocale;
        set
        {
            if (SetProperty(ref _selectedLocale, value))
            {
                RefreshInstallModes();
            }
        }
    }

    public string SelectedInstallerType
    {
        get => _selectedInstallerType;
        set
        {
            if (SetProperty(ref _selectedInstallerType, value))
            {
                RefreshInstallModes();
            }
        }
    }

    public string SelectedInstallMode
    {
        get => _selectedInstallMode;
        set
        {
            if (SetProperty(ref _selectedInstallMode, value))
            {
                RefreshCommandPreview();
            }
        }
    }

    public string InstallLocation
    {
        get => _installLocation;
        set
        {
            if (SetProperty(ref _installLocation, value))
            {
                if (!_isApplyingInstallLocationPreset)
                {
                    SetInstallLocationPresetFromCurrentValue(value);
                }

                RefreshCommandPreview();
            }
        }
    }

    public string SelectedInstallLocationPreset
    {
        get => _selectedInstallLocationPreset;
        set
        {
            if (SetProperty(ref _selectedInstallLocationPreset, value) && !string.IsNullOrWhiteSpace(value))
            {
                _isApplyingInstallLocationPreset = true;
                try
                {
                    InstallLocation = value;
                }
                finally
                {
                    _isApplyingInstallLocationPreset = false;
                }
            }
        }
    }

    public string LogPath
    {
        get => _logPath;
        set
        {
            if (SetProperty(ref _logPath, value))
            {
                RefreshCommandPreview();
            }
        }
    }

    public string AdditionalCustomArgs
    {
        get => _additionalCustomArgs;
        set
        {
            if (SetProperty(ref _additionalCustomArgs, value))
            {
                OnPropertyChanged(nameof(ShowAdvancedArgumentsWarning));
                RefreshCommandPreview();
            }
        }
    }

    public string OverrideArgs
    {
        get => _overrideArgs;
        set
        {
            if (SetProperty(ref _overrideArgs, value))
            {
                OnPropertyChanged(nameof(ShowOverrideWarning));
                OnPropertyChanged(nameof(ShowAdvancedArgumentsWarning));
                RefreshCommandPreview();
            }
        }
    }

    public void ConfigureForEditMode(bool isEditMode)
    {
        IsEditMode = isEditMode;
        RebuildArchitectureOptions(GetActiveArchitectures());
        RefreshVisibilityFlags();
    }

    public void ApplyInterrogationResult(PackageInterrogationResult result)
    {
        PackageName = result.Name;
        PackageId = result.Id;
        Version = result.Version;
        Source = result.Source;
        InstallerType = result.InstallerType;
        WarningsText = string.Join(Environment.NewLine, result.Warnings);
        IsReducedMode = result.IsReducedMode;
        _installerOptions.Clear();
        _installerOptions.AddRange(result.InstallerOptions);

        Replace(AvailableScopes, result.AvailableScopes);
        Replace(AvailableArchitectures, result.AvailableArchitectures);
        Replace(AvailableLocales, result.AvailableLocales);
        Replace(AvailableInstallerTypes, result.AvailableInstallerTypes);
        Replace(InstallLocationPresets, BuildInstallLocationPresets(result.Id));

        var defaultArchitectures = result.DefaultSelection.SelectedArchitectures.Count > 0
            ? result.DefaultSelection.SelectedArchitectures
            : string.IsNullOrWhiteSpace(result.DefaultSelection.Architecture)
                ? Array.Empty<string>()
                : new[] { result.DefaultSelection.Architecture };

        RebuildArchitectureOptions(defaultArchitectures);

        SelectedScope = result.DefaultSelection.Scope;
        SetSelectedArchitectureInternal(defaultArchitectures.FirstOrDefault() ?? result.DefaultSelection.Architecture);
        SelectedLocale = result.DefaultSelection.Locale;
        SelectedInstallerType = result.DefaultSelection.InstallerType;
        InstallLocation = result.DefaultSelection.InstallLocation;
        SetInstallLocationPresetFromCurrentValue(InstallLocation);
        LogPath = result.DefaultSelection.LogPath;
        AdditionalCustomArgs = result.DefaultSelection.AdditionalCustomArgs;
        OverrideArgs = result.DefaultSelection.OverrideArgs;

        Replace(AvailableInstallModes, result.AvailableInstallModes);
        SelectedInstallMode = result.DefaultSelection.InstallMode;
        RefreshVisibilityFlags();
        RefreshInstallModes();
        IsLoading = false;
    }

    public void ApplyExistingEntry(AppEntry entry)
    {
        ConfigureForEditMode(true);

        if (!string.IsNullOrWhiteSpace(entry.Scope) && AvailableScopes.Contains(entry.Scope))
        {
            SelectedScope = entry.Scope;
        }

        if (!string.IsNullOrWhiteSpace(entry.Architecture) && AvailableArchitectures.Contains(entry.Architecture))
        {
            SetSelectedArchitectureInternal(entry.Architecture);
            SyncArchitectureSelections(entry.Architecture);
        }

        if (!string.IsNullOrWhiteSpace(entry.Locale) && AvailableLocales.Contains(entry.Locale))
        {
            SelectedLocale = entry.Locale;
        }

        if (!string.IsNullOrWhiteSpace(entry.InstallerType) && AvailableInstallerTypes.Contains(entry.InstallerType))
        {
            SelectedInstallerType = entry.InstallerType;
        }

        if (!string.IsNullOrWhiteSpace(entry.InstallMode) && AvailableInstallModes.Contains(entry.InstallMode))
        {
            SelectedInstallMode = entry.InstallMode;
        }

        if (!string.IsNullOrWhiteSpace(entry.InstallLocation))
        {
            InstallLocation = entry.InstallLocation;
            SetInstallLocationPresetFromCurrentValue(entry.InstallLocation);
        }

        if (!string.IsNullOrWhiteSpace(entry.LogPath))
        {
            LogPath = entry.LogPath;
        }

        if (!string.IsNullOrWhiteSpace(entry.AdditionalCustomArgs))
        {
            AdditionalCustomArgs = entry.AdditionalCustomArgs;
        }

        if (!string.IsNullOrWhiteSpace(entry.OverrideArgs))
        {
            OverrideArgs = entry.OverrideArgs;
        }

        RefreshInstallModes();
    }

    public SelectedInstallOptions BuildSelection()
    {
        return BuildSelections().FirstOrDefault() ?? CreateSelection(string.Empty, Array.Empty<string>());
    }

    public IReadOnlyList<SelectedInstallOptions> BuildSelections()
    {
        var architectures = GetActiveArchitectures();
        if (architectures.Count == 0)
        {
            architectures = string.IsNullOrWhiteSpace(SelectedArchitecture)
                ? Array.Empty<string>()
                : new[] { SelectedArchitecture.Trim() };
        }

        if (architectures.Count == 0)
        {
            return new[] { CreateSelection(string.Empty, Array.Empty<string>()) };
        }

        return architectures
            .Select(architecture => CreateSelection(architecture, architectures))
            .ToList();
    }

    private void RefreshInstallModes()
    {
        if (_installerOptions.Count == 0)
        {
            OnPropertyChanged(nameof(CanConfirm));
            return;
        }

        var optionSets = GetSelectedOptionSets();
        var hasCompleteSelection = optionSets.Count > 0 && optionSets.All(set => set.Count > 0);
        var modes = new List<string>();

        if (hasCompleteSelection)
        {
            modes.Add(InstallModes.Interactive);

            if (optionSets.All(set => set.Any(option => option.SupportsSilent)))
            {
                modes.Add(InstallModes.Silent);
            }

            if (optionSets.All(set => set.Any(option => option.SupportsSilentWithProgress)))
            {
                modes.Add(InstallModes.SilentWithProgress);
            }
        }

        Replace(AvailableInstallModes, modes);
        if (!AvailableInstallModes.Contains(SelectedInstallMode))
        {
            SelectedInstallMode = AvailableInstallModes.Contains(InstallModes.SilentWithProgress)
                ? InstallModes.SilentWithProgress
                : AvailableInstallModes.Contains(InstallModes.Silent)
                    ? InstallModes.Silent
                    : AvailableInstallModes.Count > 0 ? AvailableInstallModes[0] : string.Empty;
        }

        RefreshVisibilityFlags();
    }

    private void RefreshVisibilityFlags()
    {
        OnPropertyChanged(nameof(IsFullMode));
        OnPropertyChanged(nameof(HasScopes));
        OnPropertyChanged(nameof(HasArchitectures));
        OnPropertyChanged(nameof(HasLocales));
        OnPropertyChanged(nameof(HasInstallerTypes));
        OnPropertyChanged(nameof(HasInstallModes));
        OnPropertyChanged(nameof(IsArchitectureMultiSelectVisible));
        OnPropertyChanged(nameof(IsArchitectureSingleSelectVisible));
        OnPropertyChanged(nameof(CanConfirm));
        UpdateCapabilitiesFromSelectedNodes();
        RefreshCommandPreview();
    }

    private void UpdateCapabilitiesFromSelectedNodes()
    {
        if (_installerOptions.Count == 0)
        {
            IsLocationSupported = true;
            IsLogSupported = true;
            ElevationRequirement = string.Empty;
            UnsupportedArgumentsText = string.Empty;
            return;
        }

        var optionSets = GetSelectedOptionSets();
        if (optionSets.Count == 0 || optionSets.Any(set => set.Count == 0))
        {
            IsLocationSupported = false;
            IsLogSupported = false;
            ElevationRequirement = string.Empty;
            UnsupportedArgumentsText = string.Empty;
            return;
        }

        IsLocationSupported = optionSets.All(set => set.Any(option => option.SupportsLocation));
        IsLogSupported = optionSets.All(set => set.Any(option => option.SupportsLog));

        var elevationRequirements = optionSets
            .SelectMany(set => set.Select(option => option.ElevationRequirement))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        ElevationRequirement = elevationRequirements.Count switch
        {
            0 => string.Empty,
            1 => elevationRequirements[0],
            _ => string.Join(", ", elevationRequirements)
        };

        var unsupportedArguments = optionSets
            .SelectMany(set => set.SelectMany(option => option.UnsupportedArguments))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
        UnsupportedArgumentsText = unsupportedArguments.Count == 0
            ? string.Empty
            : string.Join(", ", unsupportedArguments);
    }

    private void RefreshCommandPreview()
    {
        if (string.IsNullOrWhiteSpace(_packageId))
        {
            CommandPreview = string.Empty;
            return;
        }

        var previewLines = BuildSelections()
            .Select(BuildCommandPreviewLine)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        CommandPreview = string.Join(Environment.NewLine, previewLines);
    }

    private string BuildCommandPreviewLine(SelectedInstallOptions selection)
    {
        var sb = new StringBuilder("winget install --id ");
        sb.Append(_packageId);
        sb.Append(" -e --source ");
        sb.Append(AppEntry.NormalizeSource(_source));

        if (!string.IsNullOrWhiteSpace(selection.Scope))
        {
            sb.Append(" --scope ");
            sb.Append(selection.Scope);
        }

        if (!string.IsNullOrWhiteSpace(selection.Architecture))
        {
            sb.Append(" --architecture ");
            sb.Append(selection.Architecture);
        }

        if (!string.IsNullOrWhiteSpace(selection.InstallerType))
        {
            sb.Append(" --installer-type ");
            sb.Append(selection.InstallerType);
        }

        if (!string.IsNullOrWhiteSpace(selection.Locale))
        {
            sb.Append(" --locale ");
            sb.Append(selection.Locale);
        }

        if (IsLocationSupported && !string.IsNullOrWhiteSpace(selection.InstallLocation))
        {
            sb.Append(" --location \"");
            sb.Append(selection.InstallLocation.Replace("\"", "\\\""));
            sb.Append('"');
        }

        if (IsLogSupported && !string.IsNullOrWhiteSpace(selection.LogPath))
        {
            sb.Append(" --log \"");
            sb.Append(selection.LogPath.Replace("\"", "\\\""));
            sb.Append('"');
        }

        switch (selection.InstallMode)
        {
            case InstallModes.Silent:
                sb.Append(" --silent");
                break;
            case InstallModes.Interactive:
                sb.Append(" --interactive");
                break;
        }

        if (!string.IsNullOrWhiteSpace(selection.OverrideArgs))
        {
            sb.Append(" --override [redacted]");
        }
        else if (!string.IsNullOrWhiteSpace(selection.AdditionalCustomArgs))
        {
            sb.Append(" --custom [redacted]");
        }

        sb.Append(" --accept-package-agreements --accept-source-agreements");
        return sb.ToString();
    }

    private SelectedInstallOptions CreateSelection(string architecture, IReadOnlyList<string> selectedArchitectures)
    {
        return new SelectedInstallOptions
        {
            Scope = SelectedScope?.Trim() ?? string.Empty,
            Architecture = architecture?.Trim() ?? string.Empty,
            SelectedArchitectures = selectedArchitectures
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Locale = SelectedLocale?.Trim() ?? string.Empty,
            InstallerType = SelectedInstallerType?.Trim() ?? string.Empty,
            InstallMode = string.IsNullOrWhiteSpace(SelectedInstallMode) ? InstallModes.SilentWithProgress : SelectedInstallMode.Trim(),
            InstallLocation = IsLocationSupported ? InstallLocation?.Trim() ?? string.Empty : string.Empty,
            LogPath = IsLogSupported ? LogPath?.Trim() ?? string.Empty : string.Empty,
            SupportsInstallLocation = IsLocationSupported,
            SupportsLog = IsLogSupported,
            AdditionalCustomArgs = AdditionalCustomArgs?.Trim() ?? string.Empty,
            OverrideArgs = OverrideArgs?.Trim() ?? string.Empty,
            ElevationRequirement = _elevationRequirement?.Trim() ?? string.Empty
        };
    }

    private IReadOnlyList<List<ResolvedInstallerOption>> GetSelectedOptionSets()
    {
        if (_installerOptions.Count == 0)
        {
            return Array.Empty<List<ResolvedInstallerOption>>();
        }

        var architectures = GetActiveArchitectures();
        if (architectures.Count == 0)
        {
            return new[] { GetMatchingOptionsForArchitecture(string.Empty) };
        }

        return architectures
            .Select(GetMatchingOptionsForArchitecture)
            .ToList();
    }

    private List<ResolvedInstallerOption> GetMatchingOptionsForArchitecture(string architecture)
    {
        return _installerOptions.Where(option =>
            MatchesOptionalDimension(option.Scope, SelectedScope)
            && MatchesOptionalDimension(option.Locale, SelectedLocale)
            && Matches(option.InstallerType, SelectedInstallerType)
            && MatchesArchitecture(option.Architecture, architecture))
            .ToList();
    }

    private IReadOnlyList<string> GetActiveArchitectures()
    {
        if (IsArchitectureMultiSelectVisible)
        {
            return AvailableArchitectureOptions
                .Where(option => option.IsSelected)
                .Select(option => option.Value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return string.IsNullOrWhiteSpace(SelectedArchitecture)
            ? Array.Empty<string>()
            : new[] { SelectedArchitecture.Trim() };
    }

    private bool HasValidArchitectureSelection => !HasArchitectures || GetActiveArchitectures().Count > 0;

    private bool HasMatchingInstallerSelection
    {
        get
        {
            if (_installerOptions.Count == 0)
            {
                return true;
            }

            var optionSets = GetSelectedOptionSets();
            return optionSets.Count > 0 && optionSets.All(set => set.Count > 0);
        }
    }

    private void RebuildArchitectureOptions(IEnumerable<string> selectedArchitectures)
    {
        var selected = selectedArchitectures
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var option in AvailableArchitectureOptions)
        {
            option.PropertyChanged -= OnArchitectureOptionPropertyChanged;
        }

        _isUpdatingArchitectureSelections = true;
        try
        {
            AvailableArchitectureOptions.Clear();
            foreach (var architecture in AvailableArchitectures)
            {
                var option = new SelectableOption
                {
                    Value = architecture,
                    IsSelected = selected.Contains(architecture, StringComparer.OrdinalIgnoreCase)
                };
                option.PropertyChanged += OnArchitectureOptionPropertyChanged;
                AvailableArchitectureOptions.Add(option);
            }
        }
        finally
        {
            _isUpdatingArchitectureSelections = false;
        }
    }

    private void SyncArchitectureSelections(string selectedArchitecture)
    {
        if (_isUpdatingArchitectureSelections || !IsArchitectureSingleSelectVisible)
        {
            return;
        }

        _isUpdatingArchitectureSelections = true;
        try
        {
            foreach (var option in AvailableArchitectureOptions)
            {
                option.IsSelected = string.Equals(option.Value, selectedArchitecture, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            _isUpdatingArchitectureSelections = false;
        }
    }

    private void OnArchitectureOptionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isUpdatingArchitectureSelections || e.PropertyName != nameof(SelectableOption.IsSelected))
        {
            return;
        }

        var selectedArchitectures = AvailableArchitectureOptions
            .Where(option => option.IsSelected)
            .Select(option => option.Value)
            .ToList();

        SetSelectedArchitectureInternal(selectedArchitectures.FirstOrDefault());
        OnPropertyChanged(nameof(CanConfirm));
        RefreshInstallModes();
    }

    private void SetSelectedArchitectureInternal(string? value)
    {
        SetProperty(ref _selectedArchitecture, value?.Trim() ?? string.Empty, nameof(SelectedArchitecture));
    }

    private static void Replace(ObservableCollection<string> target, IEnumerable<string> values)
    {
        target.Clear();
        foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            target.Add(value);
        }
    }

    private static IReadOnlyList<string> BuildInstallLocationPresets(string packageId)
    {
        var safePackageId = ToSafePathSegment(packageId);
        return new[]
        {
            $@"%USERPROFILE%\Desktop\OnlyWinget Apps\{safePackageId}",
            $@"%LOCALAPPDATA%\Microsoft\WinGet\Packages\{safePackageId}",
            $@"%ProgramFiles%\WinGet\Packages\{safePackageId}",
            $@"\\server\share\OnlyWinget\{safePackageId}"
        };
    }

    private static string ToSafePathSegment(string value)
    {
        var trimmed = string.IsNullOrWhiteSpace(value) ? "Package" : value.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            trimmed = trimmed.Replace(invalid, '_');
        }

        return trimmed;
    }

    private void SetInstallLocationPresetFromCurrentValue(string value)
    {
        var preset = InstallLocationPresets.FirstOrDefault(candidate => string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        SetProperty(ref _selectedInstallLocationPreset, preset, nameof(SelectedInstallLocationPreset));
    }

    private static bool Matches(string available, string selected)
    {
        return string.IsNullOrWhiteSpace(selected)
            || string.Equals(available, selected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesOptionalDimension(string available, string selected)
    {
        return string.IsNullOrWhiteSpace(available)
            || string.IsNullOrWhiteSpace(selected)
            || string.Equals(available, selected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesArchitecture(string available, string selected)
    {
        return string.IsNullOrWhiteSpace(selected)
            || string.IsNullOrWhiteSpace(available)
            || string.Equals(available, selected, StringComparison.OrdinalIgnoreCase);
    }
}
