using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OnlyWinget.Application.App;
using OnlyWinget.Application.Storage;
using OnlyWinget.Application.System;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Application.Winget;
using OnlyWinget.Infrastructure.Storage;
using OnlyWinget.Infrastructure.Storage.Sqlite;
using OnlyWinget.Infrastructure.System;
using OnlyWinget.Infrastructure.WindowsUpdate;
using OnlyWinget.Infrastructure.Winget;
using OnlyWinget.Services;
using OnlyWinget.Shell;
using Serilog;

namespace OnlyWinget;

internal static class AppComposition
{
    private static IHost? host;

    public static IHost Host => host ??= CreateHost();

    public static IHost CreateHost()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var logDirectory = Path.Combine(root, "OnlyWinget", "logs");
        Directory.CreateDirectory(logDirectory);
        var logFilePath = Path.Combine(logDirectory, "onlywinget-.log");

        var builder = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .UseSerilog((_, loggerConfiguration) =>
            {
                loggerConfiguration
                    .MinimumLevel.Information()
                    .WriteTo.Debug()
                    .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day)
                    .WriteTo.Sink(new AppDiagnosticsSerilogSink());
            })
            .ConfigureServices((_, services) =>
            {
                // UI Services
                services.AddSingleton(sp => new JsonAppSettingsService(JsonAppSettingsService.DefaultFilePath));
                services.AddSingleton<IAppSettingsService>(sp => sp.GetRequiredService<JsonAppSettingsService>());
                services.AddSingleton(sp => new ConfirmationService(sp.GetRequiredService<JsonAppSettingsService>()));
                services.AddSingleton<FilePickerService>();
                services.AddSingleton<ClipboardService>();
                services.AddSingleton<NavigationRegistry>();
                services.AddSingleton<UiServiceCollection>(sp => new UiServiceCollection(
                    sp.GetRequiredService<JsonAppSettingsService>(),
                    sp.GetRequiredService<ConfirmationService>(),
                    sp.GetRequiredService<FilePickerService>(),
                    sp.GetRequiredService<ClipboardService>(),
                    sp.GetRequiredService<NavigationRegistry>()));

                // Infrastructure & Application Services
                services.AddSingleton<IExternalProcessRunner, ProcessExternalProcessRunner>();
                services.AddSingleton<WingetProgressParser>();
                services.AddSingleton<WingetTableParser>();
                services.AddSingleton<WingetErrorClassifier>();
                services.AddSingleton<WingetCommandBuilder>();

                services.AddSingleton<IWingetCommandRunner>(sp => new ProcessWingetCommandRunner(
                    sp.GetRequiredService<IExternalProcessRunner>(),
                    sp.GetRequiredService<WingetProgressParser>(),
                    sp.GetService<ILogger<ProcessWingetCommandRunner>>()));

                services.AddSingleton<ISystemCapabilityService, SystemCapabilityService>();

                services.AddSingleton<IWorkspaceStore>(sp => new SqliteWorkspaceStore(
                    SqliteWorkspaceStore.DefaultFilePath,
                    JsonWorkspaceStore.DefaultFilePath,
                    AppDiagnostics.WriteException,
                    sp.GetService<ILogger<SqliteWorkspaceStore>>()));

                services.AddSingleton<ISourcePreferenceStore>(sp => new JsonSourcePreferenceStore(
                    JsonSourcePreferenceStore.DefaultFilePath,
                    AppDiagnostics.WriteException,
                    sp.GetService<ILogger<JsonSourcePreferenceStore>>()));

                services.AddSingleton<WingetPackageSearchService>();
                services.AddSingleton<WingetPackageResolver>();
                services.AddSingleton<PowerShellWindowsUpdateService>();

                services.AddSingleton<IPackageSearchService, ComWingetPackageService>();
                services.AddSingleton<IPackageResolver, ComWingetPackageService>();
                services.AddSingleton<IUpdateLoader, WingetUpdateLoader>();
                services.AddSingleton<IWindowsUpdateService, ComWindowsUpdateService>();
                services.AddSingleton<IWingetSourceService, WingetSourceService>();
                services.AddSingleton<IOperationExecutor, WingetOperationExecutor>();

                // OnlyWinget Workflow App
                services.AddSingleton<OnlyWingetApplication>(sp =>
                {
                    var app = new OnlyWingetApplication(
                        sp.GetRequiredService<IWorkspaceStore>(),
                        sp.GetRequiredService<ISystemCapabilityService>(),
                        sp.GetRequiredService<IPackageSearchService>(),
                        sp.GetRequiredService<IPackageResolver>(),
                        sp.GetRequiredService<IUpdateLoader>(),
                        sp.GetRequiredService<IWindowsUpdateService>(),
                        sp.GetRequiredService<IWingetSourceService>(),
                        sp.GetRequiredService<IOperationExecutor>(),
                        sourcePreferenceStore: sp.GetRequiredService<ISourcePreferenceStore>(),
                        appLogger: sp.GetService<ILogger<OnlyWingetApplication>>());

                    app.ExceptionLogger = AppDiagnostics.WriteException;
                    app.Logger = AppDiagnostics.Write;
                    app.StateChanged += (_, _) => CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new Presentation.StateChangedMessage(app.State));
                    return app;
                });
            });

        return builder.Build();
    }

    public static UiServiceCollection CreateUiServices() => Host.Services.GetRequiredService<UiServiceCollection>();

    public static OnlyWingetApplication CreateWorkflow() => Host.Services.GetRequiredService<OnlyWingetApplication>();
}
