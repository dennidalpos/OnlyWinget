// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

namespace OnlyWinget.Models;

public sealed class AppEntry : ObservableObject
{
    internal const string DefaultSource = "winget";

    private readonly RowStatusState _statusState = new();
    private bool _isSelected = true;
    private string _name = string.Empty;
    private string _id = string.Empty;
    private string _source = DefaultSource;
    private string _action = AppActions.Install;
    private string _scope = string.Empty;
    private string _installMode = InstallModes.SilentWithProgress;
    private string _architecture = string.Empty;
    private string _locale = string.Empty;
    private string _installerType = string.Empty;
    private string _installLocation = string.Empty;
    private string _logPath = string.Empty;
    private bool _supportsInstallLocation = true;
    private bool _supportsLog = true;
    private string _additionalCustomArgs = string.Empty;
    private string _overrideArgs = string.Empty;
    private bool _advancedArgumentsReviewed = true;
    private string _elevationRequirement = string.Empty;
    private string _errorMessage = string.Empty;
    private string _resolution = string.Empty;
    private string _status = string.Empty;

    public AppEntry()
    {
        _statusState.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RowStatusState.Text))
            {
                SetProperty(ref _status, _statusState.Text, nameof(Status));
                return;
            }

            if (e.PropertyName == nameof(RowStatusState.BadgeKey))
            {
                OnPropertyChanged(nameof(StatusBadgeKey));
                return;
            }

            if (e.PropertyName == nameof(RowStatusState.BadgeSymbol))
            {
                OnPropertyChanged(nameof(StatusBadgeSymbol));
            }
        };
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Source
    {
        get => _source;
        set => SetProperty(ref _source, value);
    }

    public string Action
    {
        get => _action;
        set => SetProperty(ref _action, value);
    }

    public string Scope
    {
        get => _scope;
        set => SetProperty(ref _scope, value);
    }

    public string InstallMode
    {
        get => _installMode;
        set => SetProperty(ref _installMode, value);
    }

    public string Architecture
    {
        get => _architecture;
        set => SetProperty(ref _architecture, value);
    }

    public string OperationKey => BuildOperationKey(Id, Source, Architecture);

    public string Locale
    {
        get => _locale;
        set => SetProperty(ref _locale, value);
    }

    public string InstallerType
    {
        get => _installerType;
        set => SetProperty(ref _installerType, value);
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

    public bool SupportsInstallLocation
    {
        get => _supportsInstallLocation;
        set => SetProperty(ref _supportsInstallLocation, value);
    }

    public bool SupportsLog
    {
        get => _supportsLog;
        set => SetProperty(ref _supportsLog, value);
    }

    public string AdditionalCustomArgs
    {
        get => _additionalCustomArgs;
        set
        {
            if (SetProperty(ref _additionalCustomArgs, value))
            {
                RaiseAdvancedArgumentsStateChanged();
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
                RaiseAdvancedArgumentsStateChanged();
            }
        }
    }

    public bool AdvancedArgumentsReviewed
    {
        get => _advancedArgumentsReviewed;
        set
        {
            if (SetProperty(ref _advancedArgumentsReviewed, value))
            {
                OnPropertyChanged(nameof(RequiresAdvancedArgumentsReview));
            }
        }
    }

    public bool HasAdvancedArguments =>
        !string.IsNullOrWhiteSpace(AdditionalCustomArgs)
        || !string.IsNullOrWhiteSpace(OverrideArgs);

    public bool RequiresAdvancedArgumentsReview => HasAdvancedArguments && !AdvancedArgumentsReviewed;

    public string ElevationRequirement
    {
        get => _elevationRequirement;
        set => SetProperty(ref _elevationRequirement, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public string Resolution
    {
        get => _resolution;
        set => SetProperty(ref _resolution, value);
    }

    public string Status
    {
        get => _status;
        set => _statusState.SetRawText(value);
    }

    public UiStatusKey? StatusBadgeKey => _statusState.BadgeKey;

    public string StatusBadgeSymbol => _statusState.BadgeSymbol;

    public void ApplyStatus(UiStatusState statusState, Services.LocalizedStrings strings)
    {
        _statusState.Apply(statusState, strings);
    }

    public void RefreshLocalizedStatus(Services.LocalizedStrings strings)
    {
        _statusState.Refresh(strings);
    }

    public static string BuildOperationKey(string? id, string? source, string? architecture)
    {
        var normalizedId = (id ?? string.Empty).Trim();
        var normalizedSource = NormalizeSource(source);
        var normalizedArchitecture = (architecture ?? string.Empty).Trim();
        var idAndSource = $"{normalizedId}|{normalizedSource}";
        return string.IsNullOrWhiteSpace(normalizedArchitecture)
            ? idAndSource
            : $"{idAndSource}|{normalizedArchitecture}";
    }

    internal static string NormalizeSource(string? source)
    {
        var normalizedSource = (source ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(normalizedSource) ? DefaultSource : normalizedSource;
    }

    private void RaiseAdvancedArgumentsStateChanged()
    {
        OnPropertyChanged(nameof(HasAdvancedArguments));
        OnPropertyChanged(nameof(RequiresAdvancedArgumentsReview));
    }
}
