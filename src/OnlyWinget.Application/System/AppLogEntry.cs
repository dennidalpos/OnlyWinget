namespace OnlyWinget.Application.System;

public sealed record AppLogEntry(
    DateTimeOffset Timestamp,
    AppLogLevel Level,
    string Caller,
    string Message)
{
    public string FormattedTimestamp => Timestamp.ToString("HH:mm:ss.fff");
}
