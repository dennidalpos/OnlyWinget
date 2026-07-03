using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace OnlyWinget.Features.Settings;

public sealed partial class SettingsPage : Page
{
    private bool isInitializing;
    private readonly SettingsViewModel viewModel;

    public SettingsPage()
    {
        isInitializing = true;
        InitializeComponent();
        viewModel = new(App.UiServices.Settings);
        LoadViewModel();
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
        ConfirmDestructiveText.Text = TextResources.Get("Settings_ConfirmDestructive");
        DiagnosticsText.Text = TextResources.Get("Settings_Diagnostics");
        InstallBehaviorText.Text = TextResources.Get("Settings_InstallBehavior");
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

        isInitializing = true;
        try
        {
            await viewModel.ResetAsync(CancellationToken.None);
            LoadViewModel();
        }
        finally
        {
            isInitializing = false;
        }
    }

    private void LoadViewModel()
    {
        SelectByTag(LanguagePicker, viewModel.Language);
        SelectByTag(ThemePicker, viewModel.Theme);
        ConfirmDestructiveToggle.IsOn = viewModel.ConfirmDestructiveActions;
        DiagnosticsToggle.IsOn = viewModel.DiagnosticLogging;
        InstallBehaviorToggle.IsOn = viewModel.ContinueOperationsAfterFailure;
    }

    private Task SaveSettingsAsync()
    {
        viewModel.Language = SelectedTag(LanguagePicker, "system");
        viewModel.Theme = SelectedTag(ThemePicker, "default");
        viewModel.ConfirmDestructiveActions = ConfirmDestructiveToggle.IsOn;
        viewModel.DiagnosticLogging = DiagnosticsToggle.IsOn;
        viewModel.ContinueOperationsAfterFailure = InstallBehaviorToggle.IsOn;
        return viewModel.SaveAsync(CancellationToken.None);
    }

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
