// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

namespace OnlyWinget.Models;

public sealed class AppEntry : ObservableObject
{
    private string _name = string.Empty;
    private string _id = string.Empty;
    private string _source = "winget";
    private string _version = string.Empty;
    private string _action = AppActions.Install;
    private string _scope = string.Empty;
    private string _installMode = InstallModes.SilentWithProgress;
    private string _architecture = string.Empty;
    private string _locale = string.Empty;
    private string _installerType = string.Empty;
    private string _installLocation = string.Empty;
    private string _logPath = string.Empty;
    private string _additionalCustomArgs = string.Empty;
    private string _overrideArgs = string.Empty;
    private string _manifestFingerprint = string.Empty;
    private string _interrogatedAtUtc = string.Empty;
    private string _elevationRequirement = string.Empty;
    private string _errorMessage = string.Empty;
    private string _resolution = string.Empty;
    private string _status = string.Empty;
    private UiStatusKey _statusKey = UiStatusKey.None;
    private int? _statusProgressPercentage;
    private string _statusRawText = string.Empty;

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

    public string Version
    {
        get => _version;
        set => SetProperty(ref _version, value);
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

    public string OperationKey => BuildOperationKey(Id, Architecture);

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

    public string AdditionalCustomArgs
    {
        get => _additionalCustomArgs;
        set => SetProperty(ref _additionalCustomArgs, value);
    }

    public string OverrideArgs
    {
        get => _overrideArgs;
        set => SetProperty(ref _overrideArgs, value);
    }

    public string ManifestFingerprint
    {
        get => _manifestFingerprint;
        set => SetProperty(ref _manifestFingerprint, value);
    }

    public string InterrogatedAtUtc
    {
        get => _interrogatedAtUtc;
        set => SetProperty(ref _interrogatedAtUtc, value);
    }

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
            SetProperty(ref _status, _statusRawText);
        }
    }

    public UiStatusKey? StatusBadgeKey => _statusKey == UiStatusKey.None && string.IsNullOrWhiteSpace(_statusRawText)
        ? null
        : _statusKey;

    public void ApplyStatus(UiStatusState statusState, Services.LocalizedStrings strings)
    {
        _statusKey = statusState?.Key ?? UiStatusKey.None;
        _statusProgressPercentage = statusState?.ProgressPercentage;
        _statusRawText = statusState?.RawText ?? string.Empty;
        OnPropertyChanged(nameof(StatusBadgeKey));
        SetProperty(ref _status, BuildStatusText(strings), nameof(Status));
    }

    public void RefreshLocalizedStatus(Services.LocalizedStrings strings)
    {
        SetProperty(ref _status, BuildStatusText(strings), nameof(Status));
    }

    private string BuildStatusText(Services.LocalizedStrings strings)
    {
        if (!string.IsNullOrWhiteSpace(_statusRawText))
        {
            return _statusRawText;
        }

        var baseText = _statusKey switch
        {
            UiStatusKey.Ok => strings.StatusOk,
            UiStatusKey.Paused => strings.StatusPaused,
            UiStatusKey.UpgradeInProgress => strings.StatusUpgradeInProgress,
            UiStatusKey.AlreadyUpdated => strings.StatusAlreadyUpdated,
            UiStatusKey.InstallInProgress => strings.StatusInstallInProgress,
            UiStatusKey.AlreadyInstalled => strings.StatusAlreadyInstalled,
            UiStatusKey.UninstallInProgress => strings.StatusUninstallInProgress,
            _ => string.Empty
        };

        return _statusProgressPercentage.HasValue && !string.IsNullOrWhiteSpace(baseText)
            ? $"{baseText} {_statusProgressPercentage.Value}%"
            : baseText;
    }

    public static string BuildOperationKey(string? id, string? architecture)
    {
        var normalizedId = (id ?? string.Empty).Trim();
        var normalizedArchitecture = (architecture ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(normalizedArchitecture)
            ? normalizedId
            : $"{normalizedId}|{normalizedArchitecture}";
    }
}
