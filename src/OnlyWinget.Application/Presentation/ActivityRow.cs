using OnlyWinget.Application.Activity;

namespace OnlyWinget.Application.Presentation;

public sealed record ActivityRow(
    DateTimeOffset Timestamp,
    string TimestampDisplay,
    ActivitySeverity Severity,
    string Title,
    string Message);
