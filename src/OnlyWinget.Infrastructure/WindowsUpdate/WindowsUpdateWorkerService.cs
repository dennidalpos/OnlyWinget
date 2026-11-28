using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using OnlyWinget.Application.System;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Application.Winget;
using OnlyWinget.Infrastructure.System;

namespace OnlyWinget.Infrastructure.WindowsUpdate;

[SupportedOSPlatform("windows")]
public sealed class WindowsUpdateWorkerService(
    ProcessExternalProcessRunner processRunner,
    ILogger<WindowsUpdateWorkerService>? logger = null) : IWindowsUpdateService, IWindowsUpdateManagementService
{
    public const string Switch = "--windows-update-worker";
    private const string ProgressMarker = "progress:";
    private const string ResultMarker = "result:";
    private const int CancelledExitCode = 2;
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ChangeTimeout = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan AbortGrace = TimeSpan.FromSeconds(30);
    private static WindowsUpdateWorkerJson Json => WindowsUpdateWorkerJson.Default;

    public Task<WindowsUpdateOperationOutcome<WindowsUpdateItem>> ScanAsync(WindowsUpdateOptions options,
        CancellationToken cancellationToken) =>
        RunAsync(new(WindowsUpdateWorkerOperation.Scan, Options: options), QueryTimeout, null,
            Json.WindowsUpdateOperationOutcomeWindowsUpdateItem, cancellationToken);

    public Task<WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>> InstallAsync(
        IReadOnlyList<WindowsUpdateIdentity> updates, WindowsUpdateOptions options, CancellationToken cancellationToken,
        IProgress<OperationProgress>? progress = null) =>
        RunAsync(new(WindowsUpdateWorkerOperation.Install, updates, options), ChangeTimeout, progress,
            Json.WindowsUpdateOperationOutcomeWindowsUpdateInstallResult, cancellationToken);

    public Task<WindowsUpdateOperationOutcome<WindowsUpdateHistoryEntry>> GetHistoryAsync(
        int maxEntries, CancellationToken cancellationToken) =>
        RunAsync(new(WindowsUpdateWorkerOperation.History, MaxEntries: maxEntries), QueryTimeout, null,
            Json.WindowsUpdateOperationOutcomeWindowsUpdateHistoryEntry, cancellationToken);

    public Task<WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>> SetHiddenAsync(
        IReadOnlyList<WindowsUpdateIdentity> updates, bool hidden, WindowsUpdateOptions options,
        CancellationToken cancellationToken) =>
        RunAsync(new(WindowsUpdateWorkerOperation.SetHidden, updates, options, Hidden: hidden), QueryTimeout, null,
            Json.WindowsUpdateOperationOutcomeWindowsUpdateInstallResult, cancellationToken);

    public Task<WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>> UninstallAsync(
        IReadOnlyList<WindowsUpdateIdentity> updates, CancellationToken cancellationToken,
        IProgress<OperationProgress>? progress = null) =>
        RunAsync(new(WindowsUpdateWorkerOperation.Uninstall, updates), ChangeTimeout, progress,
            Json.WindowsUpdateOperationOutcomeWindowsUpdateInstallResult, cancellationToken);

    private async Task<WindowsUpdateOperationOutcome<T>> RunAsync<T>(
        WindowsUpdateWorkerRequest request, TimeSpan timeout, IProgress<OperationProgress>? progress,
        JsonTypeInfo<WindowsUpdateOperationOutcome<T>> resultType, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("OnlyWinget executable path is unavailable.");
        var eventId = Guid.NewGuid().ToString("N");
        using var abort = new EventWaitHandle(false, EventResetMode.ManualReset, EventName(eventId));
        using var deadline = new CancellationTokenSource(timeout);
        using var requestAbort = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, token);
        using var stopWorker = new CancellationTokenSource();
        using var registration = requestAbort.Token.Register(() =>
        {
            stopWorker.CancelAfter(AbortGrace);
            abort.Set();
        });
        requestAbort.Token.ThrowIfCancellationRequested();

        ExternalProcessResult process;
        try
        {
            var output = progress is null ? null : new InlineProgress<string>(line =>
            {
                if (line.StartsWith(ProgressMarker, StringComparison.Ordinal))
                {
                    var update = JsonSerializer.Deserialize(line[ProgressMarker.Length..], Json.WindowsUpdateWorkerProgress);
                    if (update is not null) progress.Report(update.ToProgress());
                }
            });
            process = await processRunner.RunWithInputAsync(executable, [Switch, eventId, Environment.ProcessId.ToString(global::System.Globalization.CultureInfo.InvariantCulture)],
                JsonSerializer.Serialize(request, Json.WindowsUpdateWorkerRequest), output, stopWorker.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stopWorker.IsCancellationRequested)
        {
            return Uncertain<T>("The Windows Update worker did not finish cancellation within 30 seconds.", string.Empty);
        }

        if (process.ExitCode == CancelledExitCode && requestAbort.IsCancellationRequested)
        {
            token.ThrowIfCancellationRequested();
            return WindowsUpdateOperationOutcome<T>.Failure(
                new WindowsUpdateError("Windows Update timed out and the worker confirmed cancellation. Check the current Windows Update state before retrying."), string.Empty);
        }

        if (process.Succeeded)
        {
            var line = process.StandardOutput.Split('\n', StringSplitOptions.TrimEntries)
                .LastOrDefault(item => item.StartsWith(ResultMarker, StringComparison.Ordinal));
            try
            {
                if (line is not null && JsonSerializer.Deserialize(line[ResultMarker.Length..], resultType) is { } result)
                {
                    return result;
                }
            }
            catch (JsonException exception)
            {
                logger?.LogWarning(exception, "Windows Update worker returned an unreadable result.");
            }
        }

        return Uncertain<T>($"The Windows Update worker exited without a usable result (exit {process.ExitCode}).",
            process.StandardError);
    }

    private WindowsUpdateOperationOutcome<T> Uncertain<T>(string message, string details)
    {
        logger?.LogWarning("Windows Update worker state is uncertain. {Message} {Details}", message, details);
        return WindowsUpdateOperationOutcome<T>.Failure(new WindowsUpdateError(
            message + " Windows Update may still be active. Check its state before retrying."), details);
    }

    public static async Task<int> RunWorkerAsync(string eventId, int parentId)
    {
        if (!Guid.TryParseExact(eventId, "N", out _) ||
            !EventWaitHandle.TryOpenExisting(EventName(eventId), out var abort))
        {
            return 1;
        }

        using (abort)
        using (var cancellation = new CancellationTokenSource())
        {
            using var finished = new CancellationTokenSource();
            var parentMonitor = StopWhenParentExitsAsync(parentId, cancellation, finished.Token);
            var registration = ThreadPool.RegisterWaitForSingleObject(abort, (_, _) => cancellation.Cancel(),
                null, Timeout.InfiniteTimeSpan, executeOnlyOnce: true);
            try
            {
                Console.InputEncoding = Encoding.UTF8;
                Console.OutputEncoding = Encoding.UTF8;
                var input = await Console.In.ReadToEndAsync(cancellation.Token).ConfigureAwait(false);
                var request = JsonSerializer.Deserialize(input, Json.WindowsUpdateWorkerRequest);
                if (request is null) return 1;
                if (abort.WaitOne(0)) cancellation.Cancel();
                var token = cancellation.Token;
                token.ThrowIfCancellationRequested();

                var processRunner = new ProcessExternalProcessRunner();
                var fallback = new PowerShellWindowsUpdateService(processRunner, new SystemCapabilityService(processRunner));
                var service = new ComWindowsUpdateService(fallback);
                var management = new ComWindowsUpdateManagementService();
                var progress = new InlineProgress<OperationProgress>(update => Console.WriteLine(ProgressMarker +
                    JsonSerializer.Serialize(WindowsUpdateWorkerProgress.FromProgress(update), Json.WindowsUpdateWorkerProgress)));
                var result = request.Operation switch
                {
                    WindowsUpdateWorkerOperation.Scan => JsonSerializer.Serialize(
                        await service.ScanAsync(request.Options!, token).ConfigureAwait(false),
                        Json.WindowsUpdateOperationOutcomeWindowsUpdateItem),
                    WindowsUpdateWorkerOperation.Install => JsonSerializer.Serialize(
                        await service.InstallAsync(request.Updates!, request.Options!, token, progress).ConfigureAwait(false),
                        Json.WindowsUpdateOperationOutcomeWindowsUpdateInstallResult),
                    WindowsUpdateWorkerOperation.History => JsonSerializer.Serialize(
                        await management.GetHistoryAsync(request.MaxEntries, token).ConfigureAwait(false),
                        Json.WindowsUpdateOperationOutcomeWindowsUpdateHistoryEntry),
                    WindowsUpdateWorkerOperation.SetHidden => JsonSerializer.Serialize(
                        await management.SetHiddenAsync(request.Updates!, request.Hidden, request.Options!, token).ConfigureAwait(false),
                        Json.WindowsUpdateOperationOutcomeWindowsUpdateInstallResult),
                    WindowsUpdateWorkerOperation.Uninstall => JsonSerializer.Serialize(
                        await management.UninstallAsync(request.Updates!, token, progress).ConfigureAwait(false),
                        Json.WindowsUpdateOperationOutcomeWindowsUpdateInstallResult),
                    _ => throw new InvalidOperationException("Unknown Windows Update worker operation.")
                };
                Console.WriteLine(ResultMarker + result);
                return 0;
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                return CancelledExitCode;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                Console.Error.WriteLine(exception.Message);
                return 1;
            }
            finally
            {
                await finished.CancelAsync().ConfigureAwait(false);
                await parentMonitor.ConfigureAwait(false);
                using var wait = new ManualResetEvent(false);
                if (registration.Unregister(wait)) wait.WaitOne();
            }
        }
    }

    private static async Task StopWhenParentExitsAsync(int parentId,
        CancellationTokenSource operation, CancellationToken finished)
    {
        try
        {
            try
            {
                using var parent = Process.GetProcessById(parentId);
                await parent.WaitForExitAsync(finished).ConfigureAwait(false);
            }
            catch (ArgumentException)
            {
                // The parent exited before the child opened the process handle.
            }

            _ = operation.CancelAsync();
            await Task.Delay(AbortGrace, finished).ConfigureAwait(false);
            Environment.Exit(1);
        }
        catch (OperationCanceledException) when (finished.IsCancellationRequested)
        {
        }
    }

    private static string EventName(string id) => @"Local\OnlyWinget-WUA-Worker-" + id;
}

internal enum WindowsUpdateWorkerOperation { Scan, Install, History, SetHidden, Uninstall }

internal sealed record WindowsUpdateWorkerRequest(WindowsUpdateWorkerOperation Operation,
    IReadOnlyList<WindowsUpdateIdentity>? Updates = null, WindowsUpdateOptions? Options = null,
    int MaxEntries = 0, bool Hidden = false);

internal sealed record WindowsUpdateWorkerProgress(string PackageId, WingetProgressPhase Phase,
    int Percentage, int PackagePercentage, int CompletedPackages, int TotalPackages)
{
    public static WindowsUpdateWorkerProgress FromProgress(OperationProgress value) =>
        new(value.PackageId, value.Phase, value.Percentage, value.PackagePercentage, value.CompletedPackages, value.TotalPackages);

    public OperationProgress ToProgress() =>
        new(PackageId, Phase, Percentage, PackagePercentage, CompletedPackages, TotalPackages);
}

[JsonSerializable(typeof(WindowsUpdateWorkerRequest))]
[JsonSerializable(typeof(WindowsUpdateWorkerProgress))]
[JsonSerializable(typeof(WindowsUpdateOperationOutcome<WindowsUpdateItem>))]
[JsonSerializable(typeof(WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>))]
[JsonSerializable(typeof(WindowsUpdateOperationOutcome<WindowsUpdateHistoryEntry>))]
internal sealed partial class WindowsUpdateWorkerJson : JsonSerializerContext;
