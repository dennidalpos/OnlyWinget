namespace OnlyWinget.Application.Activity;

public sealed record ActivityEntry(
    DateTimeOffset Timestamp,
    ActivitySeverity Severity,
    string Title,
    string Message);
