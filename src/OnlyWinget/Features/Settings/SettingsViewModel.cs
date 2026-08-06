using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnlyWinget.Presentation;
using OnlyWinget.Services;

namespace OnlyWinget.Features.Settings;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsService settingsService;

    [ObservableProperty]
    private string language;

    [ObservableProperty]
    private string theme;

    [ObservableProperty]
    private bool confirmDestructiveActions;

    [ObservableProperty]
    private bool diagnosticLogging;

    [ObservableProperty]
    private string logLevel;

    [ObservableProperty]
    private bool continueOperationsAfterFailure;

    internal SettingsViewModel(IAppSettingsService settingsService)
    {
        this.settingsService = settingsService;
        language = settingsService.Current.Language;
        theme = settingsService.Current.Theme;
        confirmDestructiveActions = settingsService.Current.ConfirmDestructiveActions;
        diagnosticLogging = settingsService.Current.DiagnosticLogging;
        logLevel = settingsService.Current.LogLevel;
        continueOperationsAfterFailure = settingsService.Current.ContinueOperationsAfterFailure;
    }

    [RelayCommand]
    public Task SaveAsync(CancellationToken cancellationToken) => settingsService.SaveAsync(
        new AppSettings(
            Language,
            Theme,
            ConfirmDestructiveActions,
            DiagnosticLogging,
            LogLevel,
            ContinueOperationsAfterFailure,
            settingsService.Current.SidebarWidth),
        cancellationToken);

    [RelayCommand]
    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        await settingsService.ResetAsync(cancellationToken);
        Language = settingsService.Current.Language;
        Theme = settingsService.Current.Theme;
        ConfirmDestructiveActions = settingsService.Current.ConfirmDestructiveActions;
        DiagnosticLogging = settingsService.Current.DiagnosticLogging;
        LogLevel = settingsService.Current.LogLevel;
        ContinueOperationsAfterFailure = settingsService.Current.ContinueOperationsAfterFailure;
    }
}
