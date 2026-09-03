using Microsoft.Extensions.Logging;
using OnlyWinget.Application.Activity;
using OnlyWinget.Application.Operations;
using OnlyWinget.Application.Presets;
using OnlyWinget.Application.Storage;
using OnlyWinget.Application.System;
using OnlyWinget.Application.Winget;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Domain.Operations;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Presets;
using OnlyWinget.Domain.Selection;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("OnlyWinget.Tests")]

namespace OnlyWinget.Application.App;

public sealed partial class OnlyWingetApplication(
    IWorkspaceStore workspaceStore,
    ISystemCapabilityService capabilityService,
    IPackageSearchService packageSearch,
    IPackageResolver packageResolver,
    IUpdateLoader updateLoader,
    IWindowsUpdateService windowsUpdateService,
    IWingetSourceService sourceService,
    IOperationExecutor operationExecutor,
    TimeProvider? timeProvider = null,
    ISourcePreferenceStore? sourcePreferenceStore = null,
    Microsoft.Extensions.Logging.ILogger<OnlyWingetApplication>? appLogger = null)
{
    public bool ContinueOperationsAfterFailure { get; set; }
    public bool BypassHashValidation { get; set; }
    public int MaxPackageOperationRetries { get; set; } = 1;
    public Action<string, Exception>? ExceptionLogger { get; set; }
    public Action<AppLogLevel, string, string>? Logger { get; set; }

    private readonly PresetDocumentService presetDocuments = new();
    private readonly OperationPlanner operationPlanner = new();
    private readonly SelectionState<PackageIdentity> presetInstallSelection = new();
    private readonly SelectionState<PackageIdentity> searchSelection = new();
    private readonly SelectionState<PackageIdentity> updateSelection = new();
    private readonly SelectionState<WindowsUpdateIdentity> windowsUpdateSelection = new();
    private readonly List<PackageSearchResult> searchResults = [];
    private readonly List<PackageUpdate> updates = [];
    private readonly List<WindowsUpdateItem> windowsUpdates = [];
    private readonly List<WingetSource> sources = [];
    private readonly List<ActivityEntry> activity = [];
    private readonly List<OperationExecutionResult> lastOperationResults = [];
    private readonly Dictionary<PackageIdentity, CachedPackageResolution> packageMetadata = new();
    private static readonly TimeSpan PackageMetadataCacheDuration = TimeSpan.FromMinutes(5);
    private readonly List<WindowsUpdateInstallResult> lastWindowsUpdateResults = [];
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly ISourcePreferenceStore sourcePreferences = sourcePreferenceStore ?? new EmptySourcePreferenceStore();
    private readonly HashSet<string> disabledSources = new(StringComparer.OrdinalIgnoreCase);

    private bool defaultSourcesConfigured;
    private WorkspaceState workspace = WorkspaceState.Empty;
    private ApplicationBusyState busyState;
    private SystemCapabilities capabilities = SystemCapabilities.Unknown;
    private ClassifiedWingetError? sourceError;
    private string? userVisibleError;
    private OperationProgress? operationProgress;
    private int operationInProgress;
    private OnlyWingetState? cachedState;
    private bool isStateDirty = true;

    private readonly Lock stateLock = new();

    public OnlyWingetState State => CreateState();

    public event EventHandler? StateChanged;

    private OnlyWingetState CreateState()
    {
        lock (stateLock)
        {
            if (!isStateDirty && cachedState != null)
            {
                return cachedState;
            }

            var active = ActivePreset;
            cachedState = new OnlyWingetState(
                workspace,
                active,
                presetInstallSelection.Selected.ToArray(),
                presetInstallSelection.HeaderState,
                searchResults.ToArray(),
                searchSelection.Selected.ToArray(),
                searchSelection.HeaderState,
                updates.ToArray(),
                updateSelection.Selected.ToArray(),
                updateSelection.HeaderState,
                windowsUpdates.ToArray(),
                windowsUpdateSelection.Selected.ToArray(),
                windowsUpdateSelection.HeaderState,
                lastWindowsUpdateResults.ToArray(),
                SnapshotPackageMetadata(),
                capabilities,
                sources.OrderBy(source => source.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
                sourceError,
                activity.ToArray(),
                lastOperationResults.ToArray(),
                operationProgress,
                busyState,
                userVisibleError);

            isStateDirty = false;
            return cachedState;
        }
    }

    private CancellationTokenSource? currentOperationCts;

    public void CancelCurrentOperation()
    {
        lock (stateLock)
        {
            try
            {
                currentOperationCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    private async Task<ApplicationActionResult> RunAsync(
        ApplicationBusyState state,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task> action,
        string fallbackError)
    {
        if (Interlocked.CompareExchange(ref operationInProgress, 1, 0) != 0)
        {
            return ApplicationActionResult.Failure("Another operation is already in progress.");
        }

        // Linked so that either the caller's own token (e.g. a page-level Cancel button)
        // or CancelCurrentOperation() (the global tracker's Cancel button) can stop this operation.
        lock (stateLock)
        {
            currentOperationCts?.Dispose();
            currentOperationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        busyState = state;
        userVisibleError = null;
        operationProgress = null;
        NotifyStateChanged();
        Logger?.Invoke(AppLogLevel.Verbose, $"Starting operation {state}...", "RunAsync");
        appLogger?.LogDebug("Starting operation {State}...", state);
        try
        {
            await action(currentOperationCts.Token).ConfigureAwait(false);
            Logger?.Invoke(AppLogLevel.Verbose, $"Operation {state} completed successfully.", "RunAsync");
            appLogger?.LogDebug("Operation {State} completed successfully.", state);
            return ApplicationActionResult.Success;
        }
        catch (OperationCanceledException)
        {
            if (operationProgress != null)
            {
                operationProgress = operationProgress with { Phase = WingetProgressPhase.Failed };
            }
            Logger?.Invoke(AppLogLevel.Information, $"Operation {state} was cancelled.", "RunAsync");
            appLogger?.LogInformation("Operation {State} was cancelled.", state);
            return Fail("Operation cancelled.", state);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            if (operationProgress != null)
            {
                operationProgress = operationProgress with { Phase = WingetProgressPhase.Failed };
            }
            Logger?.Invoke(AppLogLevel.Warning, $"Operation {state} failed with user error: {exception.Message}", "RunAsync");
            appLogger?.LogWarning(exception, "Operation {State} failed with user error: {Message}", state, exception.Message);
            return Fail(exception.Message, state);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (operationProgress != null)
            {
                operationProgress = operationProgress with { Phase = WingetProgressPhase.Failed };
            }
            ExceptionLogger?.Invoke("OnlyWingetApplication.RunAsync", exception);
            Logger?.Invoke(AppLogLevel.Error, $"Operation {state} failed: {exception}", "RunAsync");
            appLogger?.LogError(exception, "Operation {State} failed.", state);
            return Fail(fallbackError, state);
        }
        finally
        {
            busyState = ApplicationBusyState.Idle;
            lock (stateLock)
            {
                currentOperationCts?.Dispose();
                currentOperationCts = null;
            }
            Interlocked.Exchange(ref operationInProgress, 0);
            NotifyStateChanged();
        }
    }

    private ApplicationActionResult Run(Action action)
    {
        userVisibleError = null;
        try
        {
            lock (stateLock)
            {
                action();
            }
            return ApplicationActionResult.Success;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return Fail(exception.Message, ApplicationBusyState.Idle);
        }
        finally
        {
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged()
    {
        lock (stateLock)
        {
            isStateDirty = true;
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private ApplicationActionResult ToggleSelection<TKey>(SelectionState<TKey> selection, TKey key)
        where TKey : notnull =>
        Run(() => selection.Toggle(key));

    private void RequireWinget()
    {
        if (!capabilities.CanUseWinget)
        {
            throw new NotSupportedException(capabilities.WingetUnavailableMessage);
        }
    }

    private void RequireWindowsUpdate()
    {
        if (!capabilities.CanUseWindowsUpdate)
        {
            throw new NotSupportedException(capabilities.WindowsUpdateUnavailableMessage);
        }
    }

    private ApplicationActionResult Fail(string error, ApplicationBusyState state = ApplicationBusyState.Idle)
    {
        if (state != ApplicationBusyState.ExecutingOperation)
        {
            userVisibleError = error;
        }
        AddActivity(ActivitySeverity.Error, "Action failed", error);
        return ApplicationActionResult.Failure(error);
    }

    private static string WindowsUpdateFingerprint(WindowsUpdateIdentity update) =>
        $"{update.UpdateId.ToUpperInvariant()}|{update.RevisionNumber}";

    private static bool PresetNameEquals(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private readonly record struct CachedPackageResolution(PackageResolution Resolution, DateTimeOffset ResolvedAt);
}
