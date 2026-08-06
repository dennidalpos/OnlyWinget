using OnlyWinget.Application.System;
using Serilog.Core;
using Serilog.Events;

namespace OnlyWinget;

internal sealed class AppDiagnosticsSerilogSink : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        var level = logEvent.Level switch
        {
            LogEventLevel.Verbose or LogEventLevel.Debug => AppLogLevel.Information,
            LogEventLevel.Information => AppLogLevel.Information,
            LogEventLevel.Warning => AppLogLevel.Warning,
            LogEventLevel.Error or LogEventLevel.Fatal => AppLogLevel.Error,
            _ => AppLogLevel.Information
        };

        var caller = logEvent.Properties.TryGetValue("SourceContext", out var sourceContextVal)
            ? sourceContextVal.ToString().Trim('"')
            : "System";

        // Shorten long namespace names for readability in log viewer
        var dotIndex = caller.LastIndexOf('.');
        if (dotIndex >= 0 && dotIndex < caller.Length - 1)
        {
            caller = caller[(dotIndex + 1)..];
        }

        var message = logEvent.RenderMessage();
        if (logEvent.Exception is not null)
        {
            message = $"{message}{Environment.NewLine}{logEvent.Exception}";
        }

        AppDiagnostics.Write(level, message, caller);
    }
}
