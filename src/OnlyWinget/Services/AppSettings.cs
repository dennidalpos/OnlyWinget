namespace OnlyWinget.Services;

internal sealed record AppSettings(
    string Language = "system",
    string Theme = "default",
    bool ConfirmDestructiveActions = true,
    bool DiagnosticLogging = true,
    string LogLevel = "Information",
    bool ContinueOperationsAfterFailure = true,
    bool BypassHashValidation = false,
    double SidebarWidth = 260.0);
