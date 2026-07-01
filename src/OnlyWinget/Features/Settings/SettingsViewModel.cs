using OnlyWinget.Presentation;
using OnlyWinget.Services;

namespace OnlyWinget.Features.Settings;

internal sealed class SettingsViewModel(IAppSettingsService settingsService) : ObservableObject
{
    private string language = settingsService.Current.Language;
    private string theme = settingsService.Current.Theme;
    private bool confirmDestructiveActions = settingsService.Current.ConfirmDestructiveActions;
    private bool diagnosticLogging = settingsService.Current.DiagnosticLogging;
    private bool continueOperationsAfterFailure = settingsService.Current.ContinueOperationsAfterFailure;

    public string Language { get => language; set => SetProperty(ref language, value); }
    public string Theme { get => theme; set => SetProperty(ref theme, value); }
    public bool ConfirmDestructiveActions { get => confirmDestructiveActions; set => SetProperty(ref confirmDestructiveActions, value); }
    public bool DiagnosticLogging { get => diagnosticLogging; set => SetProperty(ref diagnosticLogging, value); }
    public bool ContinueOperationsAfterFailure { get => continueOperationsAfterFailure; set => SetProperty(ref continueOperationsAfterFailure, value); }

    public Task SaveAsync(CancellationToken cancellationToken) => settingsService.SaveAsync(
        new AppSettings(Language, Theme, ConfirmDestructiveActions, DiagnosticLogging, ContinueOperationsAfterFailure),
        cancellationToken);

    public Task ResetAsync(CancellationToken cancellationToken) => settingsService.ResetAsync(cancellationToken);
}
