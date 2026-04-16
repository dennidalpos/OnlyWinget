// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.Globalization;
using System.IO;
using OnlyWinget.Models;
using OnlyWinget.Services;
using Xunit;

namespace OnlyWinget.Tests;

public sealed class LocalizationServiceTests
{
    [Theory]
    [InlineData("en", "en")]
    [InlineData("en-GB", "en")]
    [InlineData("en-CA", "en")]
    [InlineData("it", "it")]
    [InlineData("it-IT", "it")]
    [InlineData("fr-FR", "it")]
    public void ResolveLocale_PreservesFallbackPolicy(string culture, string expectedLocale)
    {
        using var scope = new TempSettingsScope();
        var service = new LocalizationService(
            new AppPreferencesService(scope.Root),
            () => CultureInfo.GetCultureInfo("it-IT"));

        var result = service.ResolveLocale(culture);

        Assert.Equal(expectedLocale, result);
    }

    [Fact]
    public void Constructor_UsesPersistedPreference_WhenSupported()
    {
        using var scope = new TempSettingsScope();
        var preferences = new AppPreferencesService(scope.Root);
        preferences.Save(new AppPreferences
        {
            PreferredUiLanguage = "en"
        });

        var service = new LocalizationService(preferences, () => CultureInfo.GetCultureInfo("it-IT"));

        Assert.Equal("en", service.CurrentLocale);
        Assert.Equal("Add", service.Strings.Add);
        Assert.Equal("English", service.SelectedLanguage?.DisplayName);
        Assert.Equal("English", service.SelectedLanguage?.ToString());
    }

    [Fact]
    public void Constructor_FallsBackToSystemPolicy_WhenPersistedPreferenceIsInvalid()
    {
        using var scope = new TempSettingsScope();
        var preferences = new AppPreferencesService(scope.Root);
        preferences.Save(new AppPreferences
        {
            PreferredUiLanguage = "de-DE"
        });

        var service = new LocalizationService(preferences, () => CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal("en", service.CurrentLocale);
        Assert.Equal("Install", service.Strings.Install);
    }

    [Fact]
    public void SetCurrentLocale_UpdatesStringsAndPersistsChoice()
    {
        using var scope = new TempSettingsScope();
        var preferences = new AppPreferencesService(scope.Root);
        var service = new LocalizationService(preferences, () => CultureInfo.GetCultureInfo("it-IT"));

        service.SetCurrentLocale("en-GB");

        Assert.Equal("en", service.CurrentLocale);
        Assert.Equal("Search", service.Strings.Search);
        Assert.Equal("en", preferences.Load().PreferredUiLanguage);
    }

    [Fact]
    public void Catalog_ContainsNonEmptyStrings_ForBothSupportedLanguages()
    {
        Assert.Equal("it", LocalizedStrings.Italian.LocaleCode);
        Assert.Equal("en", LocalizedStrings.English.LocaleCode);
        Assert.False(string.IsNullOrWhiteSpace(LocalizedStrings.Italian.LanguageLabel));
        Assert.False(string.IsNullOrWhiteSpace(LocalizedStrings.English.LanguageLabel));
        Assert.False(string.IsNullOrWhiteSpace(LocalizedStrings.Italian.StandardUserBadge));
        Assert.False(string.IsNullOrWhiteSpace(LocalizedStrings.English.StandardUserBadge));
        Assert.False(string.IsNullOrWhiteSpace(LocalizedStrings.Italian.PackageDialogTitle));
        Assert.False(string.IsNullOrWhiteSpace(LocalizedStrings.English.PackageDialogTitle));
        Assert.False(string.IsNullOrWhiteSpace(LocalizedStrings.Italian.SearchLoadingTitle));
        Assert.False(string.IsNullOrWhiteSpace(LocalizedStrings.English.SearchLoadingTitle));
        Assert.False(string.IsNullOrWhiteSpace(LocalizedStrings.Italian.UpdatesLoadingTitle));
        Assert.False(string.IsNullOrWhiteSpace(LocalizedStrings.English.UpdatesLoadingTitle));
        Assert.False(string.IsNullOrWhiteSpace(LocalizedStrings.Italian.UseSelectedPackagesButton));
        Assert.False(string.IsNullOrWhiteSpace(LocalizedStrings.English.UseSelectedPackagesButton));
        Assert.False(string.IsNullOrWhiteSpace(LocalizedStrings.Italian.PackageDialogDetectedInstallerTypeLabel));
        Assert.False(string.IsNullOrWhiteSpace(LocalizedStrings.English.PackageDialogDetectedInstallerTypeLabel));
        Assert.False(string.IsNullOrWhiteSpace(LocalizedStrings.Italian.PromptConfirmLabel));
        Assert.False(string.IsNullOrWhiteSpace(LocalizedStrings.English.PromptConfirmLabel));
        Assert.False(string.IsNullOrWhiteSpace(LocalizedStrings.Italian.OutputLogAutomationName));
        Assert.False(string.IsNullOrWhiteSpace(LocalizedStrings.English.OutputLogAutomationName));
    }

    private sealed class TempSettingsScope : IDisposable
    {
        public TempSettingsScope()
        {
            Root = Path.Combine(Path.GetTempPath(), "OnlyWinget.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup failures in tests.
            }
        }
    }
}
