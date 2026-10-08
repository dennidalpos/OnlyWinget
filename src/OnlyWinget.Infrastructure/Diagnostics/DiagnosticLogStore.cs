using OnlyWinget.Application.System;
using System.Globalization;
using System.Text;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("OnlyWinget")]

namespace OnlyWinget.Infrastructure.Diagnostics;

internal sealed class DiagnosticLogStore(string directory)
{
    private const int BufferCapacity = 1000;
    private static readonly Encoding LogEncoding = new UTF8Encoding(false);
    private readonly object sync = new();
    private readonly Queue<AppLogEntry> entries = new();
    private bool enabled = true;
    private AppLogLevel minimumLevel = AppLogLevel.Information;
    private string? lastError;

    public string? LastError { get { lock (sync) return lastError; } }

    public void Configure(bool isEnabled, AppLogLevel level)
    {
        lock (sync)
        {
            enabled = isEnabled;
            minimumLevel = level;
        }
    }

    public bool Write(AppLogEntry entry)
    {
        lock (sync)
        {
            if (!enabled || entry.Level < minimumLevel) return true;
            entries.Enqueue(entry);
            while (entries.Count > BufferCapacity) entries.Dequeue();
            try
            {
                Directory.CreateDirectory(directory);
                var date = entry.Timestamp.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                var path = Path.Combine(directory, $"onlywinget-{date}.log");
                File.AppendAllText(path,
                    $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{entry.Level}] [{entry.Caller}] {entry.Message}{Environment.NewLine}",
                    LogEncoding);
                lastError = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                lastError = exception.Message;
                return false;
            }
        }
    }

    public IReadOnlyList<AppLogEntry> GetRecentLogs(AppLogLevel? minLevel = null, string? filterText = null)
    {
        lock (sync)
        {
            return entries.Where(entry => (!minLevel.HasValue || entry.Level >= minLevel.Value) &&
                (string.IsNullOrWhiteSpace(filterText) || entry.Message.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
                 entry.Caller.Contains(filterText, StringComparison.OrdinalIgnoreCase))).ToArray();
        }
    }

    public bool Clear()
    {
        lock (sync)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    foreach (var path in Directory.EnumerateFiles(directory, "onlywinget-????????.log"))
                    {
                        var name = Path.GetFileNameWithoutExtension(path);
                        if (DateTime.TryParseExact(name["onlywinget-".Length..], "yyyyMMdd", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out _)) File.Delete(path);
                    }
                }
                entries.Clear();
                lastError = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                lastError = exception.Message;
                return false;
            }
        }
    }
}
