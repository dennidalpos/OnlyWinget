using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using OnlyWinget.Application.System;

namespace OnlyWinget;

internal static class AppDiagnostics
{
    private static readonly object Sync = new();
    private static readonly ConcurrentQueue<AppLogEntry> InMemoryBuffer = new();
    private const int MaxInMemoryEntries = 1000;
    private static string? logFilePath;

    public static bool IsEnabled { get; set; } = true;
    public static AppLogLevel MinLogLevel { get; set; } = AppLogLevel.Information;

    public static event Action<AppLogEntry>? LogEmitted;

    public static void Initialize()
    {
        if (logFilePath is not null)
        {
            return;
        }

        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var logDirectory = Path.Combine(root, "OnlyWinget", "logs");
        Directory.CreateDirectory(logDirectory);
        logFilePath = Path.Combine(logDirectory, $"onlywinget-{DateTimeOffset.UtcNow:yyyyMMdd}.log");
        Write("Application starting.");
    }

    public static void Register(Microsoft.UI.Xaml.Application application)
    {
        application.UnhandledException += (_, args) =>
        {
            WriteException("Application.UnhandledException", args.Exception);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                WriteException("AppDomain.UnhandledException", exception);
            }
            else
            {
                Write($"AppDomain.UnhandledException: {args.ExceptionObject}");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            WriteException("TaskScheduler.UnobservedTaskException", args.Exception);
            args.SetObserved();
        };
    }

    public static void Write(string message, [CallerMemberName] string caller = "") =>
        Write(AppLogLevel.Information, message, caller);

    public static void Write(AppLogLevel level, string message, [CallerMemberName] string caller = "")
    {
        if (!IsEnabled || level < MinLogLevel)
        {
            return;
        }

        var entry = new AppLogEntry(DateTimeOffset.Now, level, caller, message);
        InMemoryBuffer.Enqueue(entry);
        while (InMemoryBuffer.Count > MaxInMemoryEntries && InMemoryBuffer.TryDequeue(out _)) { }

        try
        {
            Initialize();
            var line = $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{entry.Level}] [{entry.Caller}] {entry.Message}{Environment.NewLine}";
            lock (Sync)
            {
                File.AppendAllText(logFilePath!, line);
            }
        }
        catch
        {
        }

        try
        {
            LogEmitted?.Invoke(entry);
        }
        catch
        {
        }
    }

    public static void WriteException(string area, Exception exception) =>
        Write(AppLogLevel.Error, $"{area}: {exception}");

    public static IReadOnlyList<AppLogEntry> GetRecentLogs(AppLogLevel? minLevel = null, string? filterText = null)
    {
        var entries = InMemoryBuffer.ToArray().AsEnumerable();
        if (minLevel.HasValue)
        {
            entries = entries.Where(e => e.Level >= minLevel.Value);
        }
        if (!string.IsNullOrWhiteSpace(filterText))
        {
            entries = entries.Where(e => e.Message.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
                                         e.Caller.Contains(filterText, StringComparison.OrdinalIgnoreCase));
        }
        return entries.ToList();
    }

    public static void ClearLogs()
    {
        while (InMemoryBuffer.TryDequeue(out _)) { }

        try
        {
            lock (Sync)
            {
                var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var logDirectory = Path.Combine(root, "OnlyWinget", "logs");
                if (Directory.Exists(logDirectory))
                {
                    foreach (var file in Directory.GetFiles(logDirectory))
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }
        catch
        {
        }
    }

    public static void OpenLog()
    {
        try
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var logDirectory = Path.Combine(root, "OnlyWinget", "logs");
            Directory.CreateDirectory(logDirectory);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = logDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open log folder: {ex}");
        }
    }
}

