using OnlyWinget.Application.System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("OnlyWinget")]

namespace OnlyWinget.Infrastructure.Diagnostics;

internal sealed class DiagnosticLogStore(string directory, long maxFileBytes = 10 * 1024 * 1024, int maxFiles = 14)
{
    private const int BufferCapacity = 1000;
    private static readonly Encoding LogEncoding = new UTF8Encoding(false);
    private static readonly Regex LogName = new(@"^onlywinget-(?<date>[0-9]{8})(?:-(?<segment>[0-9]{6}))?\.log$", RegexOptions.CultureInvariant);
    private readonly long fileSizeLimit = maxFileBytes > 0 ? maxFileBytes : throw new ArgumentOutOfRangeException(nameof(maxFileBytes));
    private readonly int fileCountLimit = maxFiles > 0 ? maxFiles : throw new ArgumentOutOfRangeException(nameof(maxFiles));
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
                var text = $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{entry.Level}] [{entry.Caller}] {entry.Message}{Environment.NewLine}";
                var byteCount = LogEncoding.GetByteCount(text);
                if (byteCount > fileSizeLimit) throw new IOException("Diagnostic entry exceeds the disk file limit; the memory entry was retained.");
                var path = GetAppendPath(date, byteCount);
                if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Refusing to append through a diagnostic log reparse point.");
                File.AppendAllText(path, text, LogEncoding);
                PruneFiles(path);
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
                    foreach (var file in GetOwnedFiles()) File.Delete(file.FullName);
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

    private FileInfo[] GetOwnedFiles() => new DirectoryInfo(directory).EnumerateFiles("onlywinget-*.log")
        .Where(file =>
        {
            var match = LogName.Match(file.Name);
            return match.Success && DateTime.TryParseExact(match.Groups["date"].Value, "yyyyMMdd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _) && (file.Attributes & FileAttributes.ReparsePoint) == 0;
        }).ToArray();

    private string GetAppendPath(string date, int byteCount)
    {
        var dailyFiles = GetOwnedFiles().Where(file => LogName.Match(file.Name).Groups["date"].Value == date)
            .OrderBy(file =>
            {
                var segment = LogName.Match(file.Name).Groups["segment"].Value;
                return segment.Length == 0 ? 0 : int.Parse(segment, CultureInfo.InvariantCulture);
            }).ToArray();
        var latest = dailyFiles.LastOrDefault();
        if (latest is null) return Path.Combine(directory, $"onlywinget-{date}.log");
        if (latest.Length <= fileSizeLimit - byteCount) return latest.FullName;
        var segmentText = LogName.Match(latest.Name).Groups["segment"].Value;
        var segment = segmentText.Length == 0 ? 1 : int.Parse(segmentText, CultureInfo.InvariantCulture) + 1;
        if (segment > 999999) throw new IOException("Diagnostic rolling segment limit reached.");
        return Path.Combine(directory, $"onlywinget-{date}-{segment:D6}.log");
    }

    private void PruneFiles(string activePath)
    {
        var files = GetOwnedFiles().OrderBy(file => file.LastWriteTimeUtc).ThenBy(file => file.Name, StringComparer.Ordinal).ToArray();
        var totalBytes = files.Sum(file => file.Length);
        var remaining = files.Length;
        var diskLimit = checked(fileSizeLimit * fileCountLimit);
        foreach (var file in files)
        {
            if (remaining <= fileCountLimit && totalBytes <= diskLimit) break;
            if (string.Equals(file.FullName, activePath, StringComparison.OrdinalIgnoreCase)) continue;
            File.Delete(file.FullName);
            totalBytes -= file.Length;
            remaining--;
        }
    }
}
