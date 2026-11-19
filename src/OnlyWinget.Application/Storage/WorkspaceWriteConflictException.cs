namespace OnlyWinget.Application.Storage;

public sealed class WorkspaceWriteConflictException() : InvalidOperationException(
    "The workspace changed in another instance. Export any unsaved preset you want to keep, then reopen OnlyWinget to reload the workspace.");
