// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

namespace OnlyWinget.Models;

public sealed class UpdateEntry : ObservableObject
{
    private string _name = string.Empty;
    private string _id = string.Empty;
    private string _version = string.Empty;
    private string _available = string.Empty;
    private string _source = "winget";
    private string _scope = string.Empty;
    private string _architecture = string.Empty;
    private string _locale = string.Empty;
    private string _installerType = string.Empty;
    private string _status = string.Empty;
    private string _errorMessage = string.Empty;
    private string _resolution = string.Empty;
    private bool _selected;
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

    public string Version
    {
        get => _version;
        set => SetProperty(ref _version, value);
    }

    public string Available
    {
        get => _available;
        set => SetProperty(ref _available, value);
    }

    public string Source
    {
        get => _source;
        set => SetProperty(ref _source, value);
    }

    public string Scope
    {
        get => _scope;
        set => SetProperty(ref _scope, value);
    }

    public string Architecture
    {
        get => _architecture;
        set => SetProperty(ref _architecture, value);
    }

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

    public bool Selected
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
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
}
