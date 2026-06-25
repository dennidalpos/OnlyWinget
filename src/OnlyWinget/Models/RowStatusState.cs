// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using OnlyWinget.Services;

namespace OnlyWinget.Models;

public sealed class RowStatusState : ObservableObject
{
    private UiStatusKey _key = UiStatusKey.None;
    private int? _progressPercentage;
    private string _rawText = string.Empty;
    private string _text = string.Empty;

    public string Text
    {
        get => _text;
        private set => SetProperty(ref _text, value);
    }

    public UiStatusKey? BadgeKey => _key == UiStatusKey.None && string.IsNullOrWhiteSpace(_rawText)
        ? null
        : _key;

    public string BadgeSymbol => _key switch
    {
        UiStatusKey.Ok => "\uE73E",
        UiStatusKey.Paused => "\uE769",
        UiStatusKey.UpgradeInProgress => "\uE895",
        UiStatusKey.AlreadyUpdated => "\uE930",
        UiStatusKey.InstallInProgress => "\uE895",
        UiStatusKey.AlreadyInstalled => "\uE930",
        UiStatusKey.UninstallInProgress => "\uE74D",
        _ => string.IsNullOrWhiteSpace(_rawText) ? string.Empty : "\uE946"
    };

    public void SetRawText(string? rawText)
    {
        _key = UiStatusKey.None;
        _progressPercentage = null;
        _rawText = rawText ?? string.Empty;
        RaiseBadgeChanged();
        Text = _rawText;
    }

    public void Apply(UiStatusState? statusState, LocalizedStrings strings)
    {
        _key = statusState?.Key ?? UiStatusKey.None;
        _progressPercentage = statusState?.ProgressPercentage;
        _rawText = statusState?.RawText ?? string.Empty;
        RaiseBadgeChanged();
        Refresh(strings);
    }

    public void Refresh(LocalizedStrings strings)
    {
        Text = UiStatusTextFormatter.Format(_key, _progressPercentage, _rawText, strings);
    }

    private void RaiseBadgeChanged()
    {
        OnPropertyChanged(nameof(BadgeKey));
        OnPropertyChanged(nameof(BadgeSymbol));
    }
}
