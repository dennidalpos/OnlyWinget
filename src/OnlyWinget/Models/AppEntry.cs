// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

namespace OnlyWinget.Models;

public sealed class AppEntry : ObservableObject
{
    internal const string DefaultSource = "winget";

    private bool _enabled = true;
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
    private UiStatusKey _statusKey = UiStatusKey.None;
    private int? _statusProgressPercentage;
    private string _statusRawText = string.Empty;

    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
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
        set
        {
            _statusKey = UiStatusKey.None;
            _statusProgressPercentage = null;
            _statusRawText = value ?? string.Empty;
            OnPropertyChanged(nameof(StatusBadgeKey));
            OnPropertyChanged(nameof(StatusBadgeSymbol));
            SetProperty(ref _status, _statusRawText);
        }
    }

    public UiStatusKey? StatusBadgeKey => _statusKey == UiStatusKey.None && string.IsNullOrWhiteSpace(_statusRawText)
        ? null
        : _statusKey;

    public string StatusBadgeSymbol => _statusKey switch
    {
        UiStatusKey.Ok => "\uE73E",
        UiStatusKey.Paused => "\uE769",
        UiStatusKey.UpgradeInProgress => "\uE895",
        UiStatusKey.AlreadyUpdated => "\uE930",
        UiStatusKey.InstallInProgress => "\uE895",
        UiStatusKey.AlreadyInstalled => "\uE930",
        UiStatusKey.UninstallInProgress => "\uE74D",
        _ => string.IsNullOrWhiteSpace(_statusRawText) ? string.Empty : "\uE946"
    };

    public void ApplyStatus(UiStatusState statusState, Services.LocalizedStrings strings)
    {
        _statusKey = statusState?.Key ?? UiStatusKey.None;
        _statusProgressPercentage = statusState?.ProgressPercentage;
        _statusRawText = statusState?.RawText ?? string.Empty;
        OnPropertyChanged(nameof(StatusBadgeKey));
        OnPropertyChanged(nameof(StatusBadgeSymbol));
        SetProperty(ref _status, BuildStatusText(strings), nameof(Status));
    }

    public void RefreshLocalizedStatus(Services.LocalizedStrings strings)
    {
        SetProperty(ref _status, BuildStatusText(strings), nameof(Status));
    }

    private string BuildStatusText(Services.LocalizedStrings strings)
    {
        return UiStatusTextFormatter.Format(_statusKey, _statusProgressPercentage, _statusRawText, strings);
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
