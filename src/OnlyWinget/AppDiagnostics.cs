using OnlyWinget.Infrastructure.Diagnostics;
using System.Runtime.CompilerServices;
using OnlyWinget.Application.System;

namespace OnlyWinget;

internal static class AppDiagnostics
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OnlyWinget", "logs");
    private static readonly DiagnosticLogStore Store = new(LogDirectory);

    public static string? LastError => Store.LastError;
    public static void Configure(bool enabled, AppLogLevel level) => Store.Configure(enabled, level);
    public static void Initialize() => Write("Application starting.");
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

    public static void Write(AppLogLevel level, string message, [CallerMemberName] string caller = "") =>
        Store.Write(new AppLogEntry(DateTimeOffset.Now, level, caller, message));

    public static void Accept(AppLogEntry entry) => Store.Write(entry);

    public static void WriteException(string area, Exception exception) =>
        Write(AppLogLevel.Error, $"{area}: {exception}");

    public static IReadOnlyList<AppLogEntry> GetRecentLogs(AppLogLevel? minLevel = null, string? filterText = null) =>
        Store.GetRecentLogs(minLevel, filterText);

    public static bool ClearLogs() => Store.Clear();
    public static void OpenLog()
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = LogDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            WriteException("AppDiagnostics.OpenLog", ex);
        }
    }
}

