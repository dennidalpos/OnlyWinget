using OnlyWinget.Presentation;
using OnlyWinget.Services;

namespace OnlyWinget.Features.Settings;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsService settingsService;
    private string language;
    private string theme;
    private bool confirmDestructiveActions;
    private bool diagnosticLogging;
    private string logLevel;
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

    public string Language { get => language; set => SetProperty(ref language, value); }
    public string Theme { get => theme; set => SetProperty(ref theme, value); }
    public bool ConfirmDestructiveActions { get => confirmDestructiveActions; set => SetProperty(ref confirmDestructiveActions, value); }
    public bool DiagnosticLogging { get => diagnosticLogging; set => SetProperty(ref diagnosticLogging, value); }
    public string LogLevel { get => logLevel; set => SetProperty(ref logLevel, value); }
    public bool ContinueOperationsAfterFailure { get => continueOperationsAfterFailure; set => SetProperty(ref continueOperationsAfterFailure, value); }

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
