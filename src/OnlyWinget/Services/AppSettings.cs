namespace OnlyWinget.Services;

internal sealed record AppSettings(
    string Language = "system",
    string Theme = "default",
    bool ConfirmDestructiveActions = true,
    bool DiagnosticLogging = true,
    bool ContinueOperationsAfterFailure = false);
