// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using OnlyWinget.Models;

namespace OnlyWinget.Services;

public sealed class LocalizationService : ObservableObject
{
    private readonly AppPreferencesService _preferencesService;
    private readonly Func<CultureInfo> _systemCultureProvider;
    private readonly Dictionary<string, LocalizedStrings> _catalog;
    private string _currentLocale = "it";
    private LocalizedStrings _strings = LocalizedStrings.Italian;
    private UiLanguageOption? _selectedLanguage;

    public LocalizationService(
        AppPreferencesService? preferencesService = null,
        Func<CultureInfo>? systemCultureProvider = null)
    {
        _preferencesService = preferencesService ?? new AppPreferencesService();
        _systemCultureProvider = systemCultureProvider ?? (() => CultureInfo.CurrentUICulture);
        SupportedLanguages = new ObservableCollection<UiLanguageOption>
        {
            new() { Code = "it", DisplayName = "Italiano" },
            new() { Code = "en", DisplayName = "English" }
        };

        _catalog = new Dictionary<string, LocalizedStrings>(StringComparer.OrdinalIgnoreCase)
        {
            ["it"] = LocalizedStrings.Italian,
            ["en"] = LocalizedStrings.English
        };

        Initialize();
    }

    public ObservableCollection<UiLanguageOption> SupportedLanguages { get; }

    public string CurrentLocale => _currentLocale;

    public CultureInfo CurrentCulture => CultureInfo.GetCultureInfo(_currentLocale);

    public LocalizedStrings Strings => _strings;

    public UiLanguageOption? SelectedLanguage => _selectedLanguage;

    public void Initialize()
    {
        var preferences = _preferencesService.Load();
        var startupLocale = ResolveStartupLocale(preferences.PreferredUiLanguage);
        ApplyLocale(startupLocale, persist: false);
    }

    public LocalizedStrings GetStrings(string? culture = null)
    {
        return GetStringsForLocaleCode(ResolveLocale(culture));
    }

    public string ResolveLocale(string? culture = null)
    {
        if (TryNormalizeSupportedLocale(culture, out var localeCode))
        {
            return localeCode;
        }

        if (string.IsNullOrWhiteSpace(culture))
        {
            return ResolveDefaultLocale(_systemCultureProvider());
        }

        try
        {
            return ResolveDefaultLocale(CultureInfo.GetCultureInfo(culture));
        }
        catch (CultureNotFoundException)
        {
            return ResolveDefaultLocale(_systemCultureProvider());
        }
    }

    public void SetCurrentLocale(string? localeCode)
    {
        var resolvedLocale = TryNormalizeSupportedLocale(localeCode, out var normalized)
            ? normalized
            : ResolveLocale(localeCode);

        ApplyLocale(resolvedLocale, persist: true);
    }

    public UiLanguageOption? GetLanguageOption(string localeCode)
    {
        foreach (var option in SupportedLanguages)
        {
            if (string.Equals(option.Code, localeCode, StringComparison.OrdinalIgnoreCase))
            {
                return option;
            }
        }

        return null;
    }

    private string ResolveStartupLocale(string? preferredUiLanguage)
    {
        if (TryNormalizeSupportedLocale(preferredUiLanguage, out var persistedLocale))
        {
            return persistedLocale;
        }

        return ResolveDefaultLocale(_systemCultureProvider());
    }

    private LocalizedStrings GetStringsForLocaleCode(string localeCode)
    {
        return _catalog.TryGetValue(localeCode, out var localizedStrings)
            ? localizedStrings
            : LocalizedStrings.Italian;
    }

    private void ApplyLocale(string localeCode, bool persist)
    {
        var normalizedLocale = TryNormalizeSupportedLocale(localeCode, out var supportedLocale)
            ? supportedLocale
            : ResolveDefaultLocale(_systemCultureProvider());

        ApplyCulture(normalizedLocale);

        var strings = GetStringsForLocaleCode(normalizedLocale);
        var language = GetLanguageOption(normalizedLocale);
        var localeChanged = SetProperty(ref _currentLocale, normalizedLocale, nameof(CurrentLocale));
        var selectedChanged = SetProperty(ref _selectedLanguage, language, nameof(SelectedLanguage));
        var stringsChanged = SetProperty(ref _strings, strings, nameof(Strings));

        if (localeChanged || stringsChanged || selectedChanged)
        {
            OnPropertyChanged(nameof(CurrentCulture));
        }

        if (persist)
        {
            _preferencesService.Save(new AppPreferences
            {
                PreferredUiLanguage = normalizedLocale
            });
        }
    }

    private static void ApplyCulture(string localeCode)
    {
        var culture = CultureInfo.GetCultureInfo(localeCode);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    private static bool TryNormalizeSupportedLocale(string? localeCode, out string normalizedLocale)
    {
        normalizedLocale = string.Empty;
        if (string.IsNullOrWhiteSpace(localeCode))
        {
            return false;
        }

        var trimmed = localeCode.Trim();
        if (string.Equals(trimmed, "it", StringComparison.OrdinalIgnoreCase))
        {
            normalizedLocale = "it";
            return true;
        }

        if (string.Equals(trimmed, "en", StringComparison.OrdinalIgnoreCase))
        {
            normalizedLocale = "en";
            return true;
        }

        try
        {
            var culture = CultureInfo.GetCultureInfo(trimmed);
            normalizedLocale = culture.TwoLetterISOLanguageName switch
            {
                "en" => "en",
                "it" => "it",
                _ => string.Empty
            };
            return !string.IsNullOrWhiteSpace(normalizedLocale);
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }

    private static string ResolveDefaultLocale(CultureInfo culture)
    {
        return string.Equals(culture.TwoLetterISOLanguageName, "en", StringComparison.OrdinalIgnoreCase)
            ? "en"
            : "it";
    }
}
