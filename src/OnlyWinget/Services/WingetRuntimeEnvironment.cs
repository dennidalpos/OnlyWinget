// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System;
using System.IO;
using System.Linq;

namespace OnlyWinget.Services;

public sealed class WingetRuntimeEnvironment
{
    public const int DefaultLogRetentionDays = 30;

    private readonly string _localRuntimeRoot;
    private readonly Func<DateTime> _utcNow;

    public WingetRuntimeEnvironment(string localRuntimeRoot, Func<DateTime> utcNow)
    {
        _localRuntimeRoot = localRuntimeRoot;
        _utcNow = utcNow;
    }

    public string LogDirectory => _localRuntimeRoot;

    public string EnsureLocalRuntimeDirectory()
    {
        Directory.CreateDirectory(_localRuntimeRoot);
        return _localRuntimeRoot;
    }

    /// <summary>
    /// Deletes log files older than <paramref name="retentionDays"/> days.
    /// Does NOT delete the directory itself or recent logs.
    /// </summary>
    public void CleanupOldLogs(int retentionDays = DefaultLogRetentionDays)
    {
        if (!Directory.Exists(_localRuntimeRoot))
        {
            return;
        }

        var cutoff = _utcNow().AddDays(-Math.Max(0, retentionDays));
        try
        {
            foreach (var file in Directory.EnumerateFiles(_localRuntimeRoot, "*.log"))
            {
                try
                {
                    var lastWrite = File.GetLastWriteTimeUtc(file);
                    if (lastWrite < cutoff)
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // Skip files that cannot be deleted (e.g. locked).
                }
            }
        }
        catch
        {
            // Non bloccare il flusso applicativo per errori di cleanup.
        }
    }

    /// <summary>
    /// Legacy: kept for compatibility. Delegates to <see cref="CleanupOldLogs"/> with default retention.
    /// Does NOT delete the entire directory anymore.
    /// </summary>
    public void CleanupLocalTemp()
    {
        CleanupOldLogs(DefaultLogRetentionDays);
    }

    public string CreateOperationLogPath(string operation, string id)
    {
        var runtimeDirectory = EnsureLocalRuntimeDirectory();
        var rawName = $"{operation}-{id}-{_utcNow():yyyyMMddHHmmssfff}.log";
        var safeName = string.Join("_", rawName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        return Path.Combine(runtimeDirectory, safeName);
    }
}
