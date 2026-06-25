// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

namespace OnlyWinget.Models;

public sealed class UpdateEntry : ObservableObject
{
    private readonly RowStatusState _statusState = new();
    private string _name = string.Empty;
    private string _id = string.Empty;
    private string _version = string.Empty;
    private string _available = string.Empty;
    private string _source = AppEntry.DefaultSource;
    private string _scope = string.Empty;
    private string _architecture = string.Empty;
    private string _locale = string.Empty;
    private string _installerType = string.Empty;
    private string _status = string.Empty;
    private string _errorMessage = string.Empty;
    private string _resolution = string.Empty;
    private bool _isSelected;

    public UpdateEntry()
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

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
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
}
