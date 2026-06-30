using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Services;

namespace OnlyWinget.Features.Settings;

public sealed partial class SettingsPage : Page
{
    private bool isInitializing;

    public SettingsPage()
    {
        isInitializing = true;
        InitializeComponent();
        LoadSettings();
        ApplyText();
        isInitializing = false;
    }

    private void ApplyText()
    {
        Scaffold.Title = TextResources.Get("Settings_Title");
        Scaffold.Subtitle = TextResources.Get("Settings_Subtitle");
        LanguageTitle.Text = TextResources.Get("Settings_Language");
        LanguageDescription.Text = TextResources.Get("Settings_LanguageDescription");
        ThemeTitle.Text = TextResources.Get("Settings_Theme");
        ThemeDescription.Text = TextResources.Get("Settings_ThemeDescription");
        ConfirmDestructiveToggle.Header = TextResources.Get("Settings_ConfirmDestructive");
        DiagnosticsToggle.Header = TextResources.Get("Settings_Diagnostics");
        InstallBehaviorToggle.Header = TextResources.Get("Settings_InstallBehavior");
        ResetTitle.Text = TextResources.Get("Settings_Reset");
        ResetDescription.Text = TextResources.Get("Settings_ResetDescription");
        ResetButton.Content = TextResources.Get("Settings_ResetAction");
    }

    private async void OnThemeChanged(object sender, SelectionChangedEventArgs args)
    {
        if (isInitializing || ThemePicker.SelectedItem is not ComboBoxItem)
        {
            return;
        }

        await SaveSettingsAsync();
    }

    private async void OnLanguageChanged(object sender, SelectionChangedEventArgs args)
    {
        if (isInitializing || LanguagePicker.SelectedItem is not ComboBoxItem)
        {
            return;
        }

        await SaveSettingsAsync();
    }

    private async void OnSettingToggled(object sender, RoutedEventArgs args)
    {
        if (!isInitializing)
        {
            await SaveSettingsAsync();
        }
    }

    private async void OnReset(object sender, RoutedEventArgs args)
    {
        if (!await App.UiServices.Confirmation.ConfirmAsync(
                XamlRoot,
                "Settings_Reset",
                "Settings_ResetConfirmation"))
        {
            return;
        }

        await App.UiServices.Settings.ResetAsync(CancellationToken.None);
    }

    private void LoadSettings()
    {
        var settings = App.UiServices.Settings.Current;
        SelectByTag(LanguagePicker, settings.Language);
        SelectByTag(ThemePicker, settings.Theme);
        ConfirmDestructiveToggle.IsOn = settings.ConfirmDestructiveActions;
        DiagnosticsToggle.IsOn = settings.DiagnosticLogging;
        InstallBehaviorToggle.IsOn = settings.ContinueOperationsAfterFailure;
    }

    private Task SaveSettingsAsync() => App.UiServices.Settings.SaveAsync(
        new AppSettings(
            SelectedTag(LanguagePicker, "system"),
            SelectedTag(ThemePicker, "default"),
            ConfirmDestructiveToggle.IsOn,
            DiagnosticsToggle.IsOn,
            InstallBehaviorToggle.IsOn),
        CancellationToken.None);

    private static string SelectedTag(ComboBox comboBox, string fallback) =>
        (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? fallback;

    private static void SelectByTag(ComboBox comboBox, string tag)
    {
        comboBox.SelectedItem = comboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), tag, StringComparison.Ordinal))
            ?? comboBox.Items[0];
    }
}
