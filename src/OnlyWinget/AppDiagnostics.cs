using System.Runtime.CompilerServices;
using OnlyWinget.Application.System;

namespace OnlyWinget;

internal static class AppDiagnostics
{
    private static readonly object Sync = new();
    private static string? logFilePath;

    public static bool IsEnabled { get; set; } = true;
    public static AppLogLevel MinLogLevel { get; set; } = AppLogLevel.Information;

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

        try
        {
            Initialize();
            var line = $"{DateTimeOffset.Now.ToString(System.Globalization.CultureInfo.CurrentCulture)} [{level}] [{caller}] {message}{Environment.NewLine}";
            lock (Sync)
            {
                File.AppendAllText(logFilePath!, line);
            }
        }
        catch
        {
        }
    }

    public static void WriteException(string area, Exception exception) =>
        Write(AppLogLevel.Error, $"{area}: {exception}");

    public static void OpenLog()
    {
        try
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var logDirectory = Path.Combine(root, "OnlyWinget", "logs");
            Directory.CreateDirectory(logDirectory);

            var todayFile = Path.Combine(logDirectory, $"onlywinget-{DateTimeOffset.UtcNow:yyyyMMdd}.log");
            var fileToOpen = File.Exists(todayFile) ? todayFile : logDirectory;

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileToOpen,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open log: {ex}");
        }
    }
}
