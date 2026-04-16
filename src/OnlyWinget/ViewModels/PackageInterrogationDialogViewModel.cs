// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
    private string _selectedScope = string.Empty;
    private string _selectedArchitecture = string.Empty;
    private string _selectedLocale = string.Empty;
    private string _selectedInstallerType = string.Empty;
    private string _selectedInstallMode = InstallModes.SilentWithProgress;
    private string _installLocation = string.Empty;
    private string _logPath = string.Empty;
    private string _additionalCustomArgs = string.Empty;
    private string _overrideArgs = string.Empty;
    // Capability state derived from the selected installer node
    private bool _isLocationSupported = true;
    private bool _isLogSupported = true;
    private string _elevationRequirement = string.Empty;
    private string _unsupportedArgumentsText = string.Empty;
    private string _commandPreview = string.Empty;

    public PackageInterrogationDialogViewModel(LocalizedStrings strings)
    {
        _strings = strings;
        AvailableScopes = new ObservableCollection<string>();
        AvailableArchitectures = new ObservableCollection<string>();
        AvailableLocales = new ObservableCollection<string>();
        AvailableInstallerTypes = new ObservableCollection<string>();
        AvailableInstallModes = new ObservableCollection<string>();
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
    public string LogPathLabel => _strings.PackageDialogLogPathLabel;
    public string AdditionalCustomArgsLabel => _strings.PackageDialogAdditionalCustomArgsLabel;
    public string OverrideArgsLabel => _strings.PackageDialogOverrideArgsLabel;
    public string AdvancedTitle => _strings.PackageDialogAdvancedTitle;
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

    public bool CanConfirm => !IsLoading;

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

    public bool IsFullMode => !IsReducedMode;

    public bool IsFullModeContentReady => IsContentReady && IsFullMode;

    public ObservableCollection<string> AvailableScopes { get; }
    public ObservableCollection<string> AvailableArchitectures { get; }
    public ObservableCollection<string> AvailableLocales { get; }
    public ObservableCollection<string> AvailableInstallerTypes { get; }
    public ObservableCollection<string> AvailableInstallModes { get; }

    public bool HasScopes => AvailableScopes.Count > 0;
    public bool HasArchitectures => AvailableArchitectures.Count > 0;
    public bool HasLocales => AvailableLocales.Count > 0;
    public bool HasInstallerTypes => AvailableInstallerTypes.Count > 0;
    public bool HasInstallModes => AvailableInstallModes.Count > 0;

    // Capability-driven field state
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

    /// <summary>
    /// Shows the override warning when the user has entered override arguments.
    /// Override replaces all manifest-provided installer switches — potentially
    /// bypassing scope-specific behaviour (e.g. Inno Setup /ALLUSERS vs /CURRENTUSER).
    /// </summary>
    public bool ShowOverrideWarning => !string.IsNullOrWhiteSpace(OverrideArgs);

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
        set => SetProperty(ref _selectedInstallMode, value);
    }

    public string InstallLocation
    {
        get => _installLocation;
        set => SetProperty(ref _installLocation, value);
    }

    public string LogPath
    {
        get => _logPath;
        set => SetProperty(ref _logPath, value);
    }

    public string AdditionalCustomArgs
    {
        get => _additionalCustomArgs;
        set
        {
            if (SetProperty(ref _additionalCustomArgs, value))
            {
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
                RefreshCommandPreview();
            }
        }
    }

    public void ApplyInterrogationResult(PackageInterrogationResult result)
    {
        PackageName = result.Name;
        PackageId = result.Id;
        Version = result.Version;
        Source = result.Source;
        InstallerType = result.InstallerType;
        WarningsText = string.Join(System.Environment.NewLine, result.Warnings);
        IsReducedMode = result.IsReducedMode;
        _installerOptions.Clear();
        _installerOptions.AddRange(result.InstallerOptions);

        Replace(AvailableScopes, result.AvailableScopes);
        Replace(AvailableArchitectures, result.AvailableArchitectures);
        Replace(AvailableLocales, result.AvailableLocales);
        Replace(AvailableInstallerTypes, result.AvailableInstallerTypes);

        SelectedScope = result.DefaultSelection.Scope;
        SelectedArchitecture = result.DefaultSelection.Architecture;
        SelectedLocale = result.DefaultSelection.Locale;
        SelectedInstallerType = result.DefaultSelection.InstallerType;
        InstallLocation = result.DefaultSelection.InstallLocation;
        LogPath = result.DefaultSelection.LogPath;
        AdditionalCustomArgs = result.DefaultSelection.AdditionalCustomArgs;
        OverrideArgs = result.DefaultSelection.OverrideArgs;

        Replace(AvailableInstallModes, result.AvailableInstallModes);
        SelectedInstallMode = result.DefaultSelection.InstallMode;
        RefreshVisibilityFlags();
        RefreshInstallModes();
        IsLoading = false;
    }

    /// <summary>
    /// Overwrites the current selections with values from an already-queued entry.
    /// Called when editing rather than adding a package, after ApplyInterrogationResult.
    /// Only sets fields that are non-empty in the existing entry, so manifest defaults
    /// are preserved for any unset fields.
    /// </summary>
    public void ApplyExistingEntry(AppEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Scope) && AvailableScopes.Contains(entry.Scope))
        {
            SelectedScope = entry.Scope;
        }

        if (!string.IsNullOrWhiteSpace(entry.Architecture) && AvailableArchitectures.Contains(entry.Architecture))
        {
            SelectedArchitecture = entry.Architecture;
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
    }

    public SelectedInstallOptions BuildSelection()
    {
        return new SelectedInstallOptions
        {
            Scope = SelectedScope?.Trim() ?? string.Empty,
            Architecture = SelectedArchitecture?.Trim() ?? string.Empty,
            Locale = SelectedLocale?.Trim() ?? string.Empty,
            InstallerType = SelectedInstallerType?.Trim() ?? string.Empty,
            InstallMode = string.IsNullOrWhiteSpace(SelectedInstallMode) ? InstallModes.SilentWithProgress : SelectedInstallMode.Trim(),
            InstallLocation = InstallLocation?.Trim() ?? string.Empty,
            LogPath = LogPath?.Trim() ?? string.Empty,
            AdditionalCustomArgs = AdditionalCustomArgs?.Trim() ?? string.Empty,
            OverrideArgs = OverrideArgs?.Trim() ?? string.Empty,
            ElevationRequirement = _elevationRequirement?.Trim() ?? string.Empty
        };
    }

    private void RefreshInstallModes()
    {
        if (_installerOptions.Count == 0)
        {
            return;
        }

        var matching = _installerOptions.Where(option =>
            Matches(option.Scope, SelectedScope)
            && Matches(option.Architecture, SelectedArchitecture)
            && Matches(option.Locale, SelectedLocale)
            && Matches(option.InstallerType, SelectedInstallerType))
            .ToList();

        if (matching.Count == 0)
        {
            matching = _installerOptions;
        }

        var modes = new List<string> { InstallModes.Interactive };
        if (matching.Any(option => option.SupportsSilent))
        {
            modes.Add(InstallModes.Silent);
        }

        if (matching.Any(option => option.SupportsSilentWithProgress))
        {
            modes.Add(InstallModes.SilentWithProgress);
        }

        Replace(AvailableInstallModes, modes);
        if (!AvailableInstallModes.Contains(SelectedInstallMode))
        {
            SelectedInstallMode = AvailableInstallModes.Contains(InstallModes.SilentWithProgress)
                ? InstallModes.SilentWithProgress
                : AvailableInstallModes.Contains(InstallModes.Silent)
                    ? InstallModes.Silent
                    : AvailableInstallModes.Count > 0 ? AvailableInstallModes[0] : InstallModes.Interactive;
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
        UpdateCapabilitiesFromSelectedNode();
        RefreshCommandPreview();
    }

    private void UpdateCapabilitiesFromSelectedNode()
    {
        if (_installerOptions.Count == 0)
        {
            IsLocationSupported = true;
            IsLogSupported = true;
            ElevationRequirement = string.Empty;
            UnsupportedArgumentsText = string.Empty;
            return;
        }

        var matching = _installerOptions.Where(option =>
            Matches(option.Scope, SelectedScope)
            && Matches(option.Architecture, SelectedArchitecture)
            && Matches(option.Locale, SelectedLocale)
            && Matches(option.InstallerType, SelectedInstallerType))
            .ToList();

        var selected = matching.Count > 0 ? matching[0] : _installerOptions[0];
        IsLocationSupported = selected.SupportsLocation;
        IsLogSupported = selected.SupportsLog;
        ElevationRequirement = selected.ElevationRequirement;

        if (selected.UnsupportedArguments.Count > 0)
        {
            UnsupportedArgumentsText = string.Join(", ", selected.UnsupportedArguments);
        }
        else
        {
            UnsupportedArgumentsText = string.Empty;
        }
    }

    private void RefreshCommandPreview()
    {
        if (string.IsNullOrWhiteSpace(_packageId))
        {
            CommandPreview = string.Empty;
            return;
        }

        var sb = new System.Text.StringBuilder("winget install --id ");
        sb.Append(_packageId);
        sb.Append(" -e --source ");
        sb.Append(string.IsNullOrWhiteSpace(_source) ? "winget" : _source);

        if (!string.IsNullOrWhiteSpace(SelectedScope))
        {
            sb.Append(" --scope "); sb.Append(SelectedScope);
        }

        if (!string.IsNullOrWhiteSpace(SelectedArchitecture))
        {
            sb.Append(" --architecture "); sb.Append(SelectedArchitecture);
        }

        if (!string.IsNullOrWhiteSpace(SelectedInstallerType))
        {
            sb.Append(" --installer-type "); sb.Append(SelectedInstallerType);
        }

        if (!string.IsNullOrWhiteSpace(SelectedLocale))
        {
            sb.Append(" --locale "); sb.Append(SelectedLocale);
        }

        if (IsLocationSupported && !string.IsNullOrWhiteSpace(InstallLocation))
        {
            sb.Append(" --location \""); sb.Append(InstallLocation.Replace("\"", "\\\"")); sb.Append('"');
        }

        if (IsLogSupported && !string.IsNullOrWhiteSpace(LogPath))
        {
            sb.Append(" --log \""); sb.Append(LogPath.Replace("\"", "\\\"")); sb.Append('"');
        }

        switch (SelectedInstallMode)
        {
            case InstallModes.Silent:
                sb.Append(" --silent");
                break;
            case InstallModes.Interactive:
                sb.Append(" --interactive");
                break;
        }

        if (!string.IsNullOrWhiteSpace(OverrideArgs))
        {
            sb.Append(" --override \""); sb.Append(OverrideArgs.Replace("\"", "\\\"")); sb.Append('"');
        }
        else if (!string.IsNullOrWhiteSpace(AdditionalCustomArgs))
        {
            sb.Append(" --custom \""); sb.Append(AdditionalCustomArgs.Replace("\"", "\\\"")); sb.Append('"');
        }

        sb.Append(" --accept-package-agreements --accept-source-agreements");

        CommandPreview = sb.ToString();
    }

    private static void Replace(ObservableCollection<string> target, IEnumerable<string> values)
    {
        target.Clear();
        foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            target.Add(value);
        }
    }

    private static bool Matches(string available, string selected)
    {
        return string.IsNullOrWhiteSpace(selected)
            || string.Equals(available, selected, System.StringComparison.OrdinalIgnoreCase);
    }

}
