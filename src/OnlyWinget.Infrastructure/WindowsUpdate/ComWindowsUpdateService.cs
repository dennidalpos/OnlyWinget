using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using OnlyWinget.Application.System;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Application.Winget;

namespace OnlyWinget.Infrastructure.WindowsUpdate;

/// <summary>
/// Builds the WUApi search criteria string. Pure string logic shared by the native COM path
/// (<see cref="ComWindowsUpdateService"/>) and mirrors PowerShellWindowsUpdateService.ApplyOptions,
/// so it is kept outside the [SupportedOSPlatform("windows")] type to stay unit-testable on any OS.
/// </summary>
public static class WindowsUpdateSearchCriteria
{
    public static string Build(WindowsUpdateOptions options)
    {
        if (!options.IncludeSoftware && !options.IncludeDrivers)
        {
            throw new ArgumentException("Select software updates, drivers, or both.", nameof(options));
        }

        var typeCriteria = (options.IncludeSoftware, options.IncludeDrivers) switch
        {
            (true, false) => " and Type='Software'",
            (false, true) => " and Type='Driver'",
            _ => string.Empty
        };

        var browseCriteria = options.IncludeOptionalUpdates
            ? string.Empty
            : " and BrowseOnly=0";

        // Hidden updates are what Settings lists under "Hidden updates"; every other scan skips them.
        var hiddenCriteria = options.ShowHidden ? "IsHidden=1" : "IsHidden=0";
        return $"IsInstalled=0 and {hiddenCriteria}{typeCriteria}{browseCriteria}";
    }
}

[SupportedOSPlatform("windows")]
public sealed class ComWindowsUpdateService(
    PowerShellWindowsUpdateService fallbackService,
    ILogger<ComWindowsUpdateService>? logger = null) : IWindowsUpdateService
{
    private const string ProgId = "Microsoft.Update.Session";
    private const string MicrosoftUpdateServiceId = "7971f918-a847-4430-9279-4a52d1efe18d";
    private const int ScanAttempts = 3;

    public async Task<WindowsUpdateOperationOutcome<WindowsUpdateItem>> ScanAsync(
        WindowsUpdateOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (OperatingSystem.IsWindows())
        {
            var sessionType = Type.GetTypeFromProgID(ProgId);
            for (var attempt = 1; sessionType is not null; attempt++)
            {
                try
                {
                    var outcome = await Task.Run(() => ScanNativeComAsync(options, cancellationToken), cancellationToken).ConfigureAwait(false);
                    if (outcome is not null && outcome.Succeeded)
                    {
                        logger?.LogInformation("Windows Update COM scan completed successfully via Microsoft.Update.Session with {Count} updates.", outcome.Rows.Count);
                        return outcome;
                    }

                    break;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (WindowsUpdateComCompletionException ex)
                {
                    return WindowsUpdateOperationOutcome<WindowsUpdateItem>.Failure(new WindowsUpdateError(ex.Message), string.Empty);
                }
                catch (COMException ex) when (WindowsUpdateErrorCodes.IsTransient(ex.HResult) && attempt < ScanAttempts)
                {
                    // The service was restarting or busy (for example right after an installer ran). The PowerShell
                    // fallback drives the same API and would hit the same condition, so wait and retry first.
                    var delay = TimeSpan.FromSeconds(2 * attempt * attempt);
                    logger?.LogWarning(
                        "Windows Update COM scan hit a transient error {Description}; retrying in {Delay}s (attempt {Attempt}/{Max}).",
                        WindowsUpdateErrorCodes.Describe(ex.HResult), delay.TotalSeconds, attempt, ScanAttempts);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    var description = ex is COMException comException ? WindowsUpdateErrorCodes.Describe(comException.HResult) : null;
                    logger?.LogWarning(ex, "Windows Update COM Interop failed ({Description}). Falling back to PowerShell execution.", description ?? ex.Message);
                    break;
                }
            }
        }

        var fallback = await fallbackService.ScanAsync(options, cancellationToken).ConfigureAwait(false);
        logger?.LogInformation(
            "Windows Update PowerShell fallback scan finished: succeeded {Succeeded}, {Count} updates{Error}.",
            fallback.Succeeded, fallback.Rows.Count, fallback.Error is null ? string.Empty : $", error: {fallback.Error.Message}");
        return fallback;
    }

    public async Task<WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>> InstallAsync(
        IReadOnlyList<WindowsUpdateIdentity> updates,
        WindowsUpdateOptions options,
        CancellationToken cancellationToken,
        IProgress<OperationProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(updates);
        cancellationToken.ThrowIfCancellationRequested();

        if (OperatingSystem.IsWindows() && updates.Count > 0)
        {
            try
            {
                var sessionType = Type.GetTypeFromProgID(ProgId);
                if (sessionType is not null)
                {
                    var outcome = await Task.Run(() => InstallNativeComAsync(updates, options, progress, cancellationToken), cancellationToken).ConfigureAwait(false);
                    if (outcome is not null)
                    {
                        logger?.LogInformation("Windows Update COM installation returned {Count} results; successful outcome: {Succeeded}.", outcome.Rows.Count, outcome.Succeeded);
                        return outcome;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (COMException ex) when (ex.HResult == unchecked((int)0x80040154))
            {
                logger?.LogWarning(ex, "Windows Update COM is not registered. Falling back to PowerShell execution.");
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                logger?.LogError(ex, "Windows Update COM install failed; automatic retry is disabled to avoid repeating installation.");
                var message = ex is COMException com ? WindowsUpdateErrorCodes.Describe(com.HResult) ?? ex.Message : ex.Message;
                return WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>.Failure(new WindowsUpdateError(message), string.Empty);
            }
        }

        return await fallbackService.InstallAsync(updates, options, cancellationToken, progress).ConfigureAwait(false);
    }

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Windows Update COM dynamic invocation is protected by try-catch fallback.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Windows Update ProgID type instantiation.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Windows Update COM dynamic invocation is protected by try-catch fallback.")]
    private static async Task<WindowsUpdateOperationOutcome<WindowsUpdateItem>?> ScanNativeComAsync(WindowsUpdateOptions options, CancellationToken cancellationToken)
    {
        var sessionType = Type.GetTypeFromProgID(ProgId);
        if (sessionType is null) return null;

        object? sessionObj = null;
        object? searcherObj = null;
        object? searchResultObj = null;
        object? updateCollectionObject = null;

        try
        {
            dynamic session = Activator.CreateInstance(sessionType)!;
            sessionObj = (object)session;

            dynamic searcher = session.CreateUpdateSearcher();
            searcherObj = (object)searcher;
            TryRegisterMicrosoftUpdateService(searcher, options);

            dynamic searchResult = await RunComJobAsync(
                callback => searcher.BeginSearch(WindowsUpdateSearchCriteria.Build(options), callback, null),
                job => searcher.EndSearch(job), cancellationToken).ConfigureAwait(false);
            searchResultObj = (object)searchResult;
            if (Convert.ToInt32(searchResult.ResultCode) != 2)
            {
                throw new InvalidOperationException("Windows Update search did not complete successfully.");
            }

            dynamic updateCollection = searchResult.Updates;
            updateCollectionObject = (object)updateCollection;

            var items = new List<WindowsUpdateItem>();
            int count = updateCollection.Count;

            for (int i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                dynamic update = updateCollection.Item(i);
                try { items.Add(MapNativeUpdate((object)update)); }
                finally { TryReleaseCom((object)update); }
            }

            return WindowsUpdateOperationOutcome<WindowsUpdateItem>.Success(items, "COM Native Search Completed");
        }
        finally
        {
            TryReleaseCom(updateCollectionObject);
            TryReleaseCom(searchResultObj);
            TryReleaseCom(searcherObj);
            TryReleaseCom(sessionObj);
        }
    }

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "WUA automation metadata is supplied by the operating system.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "WUA automation requires the runtime binder.")]
    internal static WindowsUpdateItem MapNativeUpdate(object nativeUpdate)
    {
        dynamic update = nativeUpdate;
        object? identityObject = null;
        object? categoriesObject = null;
        object? articlesObject = null;
        try
        {
            dynamic identity = update.Identity;
            identityObject = (object)identity;
            dynamic categoryCollection = update.Categories;
            categoriesObject = (object)categoryCollection;
            var categories = new List<string>();
            var categoryIds = new List<string>();
            for (var index = 0; index < (int)categoryCollection.Count; index++)
            {
                object category = categoryCollection.Item(index);
                try
                {
                    string? name = ((dynamic)category).Name;
                    if (!string.IsNullOrWhiteSpace(name)) categories.Add(name);
                    try
                    {
                        string? categoryId = ((dynamic)category).CategoryID;
                        if (!string.IsNullOrWhiteSpace(categoryId)) categoryIds.Add(categoryId);
                    }
                    catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
                    {
                        // The category has no ID: classification falls back to the title.
                    }
                }
                finally { TryReleaseCom(category); }
            }

            dynamic articleCollection = update.KBArticleIDs;
            articlesObject = (object)articleCollection;
            var articles = new List<string>();
            for (var index = 0; index < (int)articleCollection.Count; index++)
            {
                string? article = articleCollection.Item(index);
                if (!string.IsNullOrWhiteSpace(article)) articles.Add(article.Trim());
            }

            string? severity = update.MsrcSeverity;
            var title = (string)update.Title;
            // Real WUA updates always expose these; only the hide/uninstall extras are optional.
            var isHidden = false;
            var isUninstallable = false;
            try { isHidden = Convert.ToBoolean(update.IsHidden); }
            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { }
            try { isUninstallable = Convert.ToBoolean(update.IsUninstallable); }
            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { }
            return new WindowsUpdateItem(
                new WindowsUpdateIdentity((string)identity.UpdateID, (int)identity.RevisionNumber),
                title,
                (string?)update.Description,
                string.IsNullOrWhiteSpace(severity) ? null : severity,
                categories,
                articles,
                Convert.ToUInt64(update.MaxDownloadSize),
                Convert.ToBoolean(update.IsDownloaded),
                Convert.ToBoolean(update.RebootRequired),
                WindowsUpdateClassifier.FromCategoryIds(categoryIds, title),
                isHidden,
                isUninstallable);
        }
        finally
        {
            TryReleaseCom(articlesObject);
            TryReleaseCom(categoriesObject);
            TryReleaseCom(identityObject);
        }
    }

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Windows Update COM dynamic invocation is protected by try-catch fallback.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Windows Update ProgID type instantiation.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Windows Update COM dynamic invocation is protected by try-catch fallback.")]
    private static async Task<WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>?> InstallNativeComAsync(
        IReadOnlyList<WindowsUpdateIdentity> targetUpdates,
        WindowsUpdateOptions options,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var sessionType = Type.GetTypeFromProgID(ProgId);
        if (sessionType is null) return null;

        object? sessionObj = null;
        object? searcherObj = null;
        object? searchResultObj = null;
        object? installCollectionObj = null;

        try
        {
            dynamic session = Activator.CreateInstance(sessionType)!;
            sessionObj = (object)session;

            dynamic searcher = session.CreateUpdateSearcher();
            searcherObj = (object)searcher;
            TryRegisterMicrosoftUpdateService(searcher, options);

            dynamic searchResult = await RunComJobAsync(
                callback => searcher.BeginSearch(WindowsUpdateSearchCriteria.Build(options), callback, null),
                job => searcher.EndSearch(job), cancellationToken).ConfigureAwait(false);
            searchResultObj = (object)searchResult;
            if (Convert.ToInt32(searchResult.ResultCode) != 2)
            {
                return WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>.Failure(
                    new WindowsUpdateError("Windows Update search did not complete successfully. Installation was not started."), string.Empty);
            }

            dynamic availableUpdates = searchResult.Updates;
            int availableCount = availableUpdates.Count;

            var targetMap = targetUpdates.ToDictionary(u => $"{u.UpdateId}|{u.RevisionNumber}", StringComparer.OrdinalIgnoreCase);
            var updateCollectionType = Type.GetTypeFromProgID("Microsoft.Update.UpdateColl");
            dynamic installCollection = Activator.CreateInstance(updateCollectionType ?? Type.GetTypeFromCLSID(new Guid("1361661A-2A21-4226-928E-2E31A2F69527"))!)!;
            installCollectionObj = (object)installCollection;

            var matchedItems = new List<(string Title, WindowsUpdateIdentity Identity)>();

            for (int i = 0; i < availableCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                dynamic update = availableUpdates.Item(i);
                dynamic identity = update.Identity;
                string updateId = identity.UpdateID?.ToString() ?? string.Empty;
                int revisionNumber = Convert.ToInt32(identity.RevisionNumber);

                if (targetMap.TryGetValue($"{updateId}|{revisionNumber}", out var targetIdent))
                {
                    installCollection.Add(update);
                    matchedItems.Add((update.Title?.ToString() ?? updateId, targetIdent));
                }
            }

            if (matchedItems.Count != targetMap.Count)
            {
                return WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>.Failure(
                    new WindowsUpdateError("Some selected Windows updates or revisions were not found. Scan again before installing."), string.Empty);
            }

            for (int i = 0; i < matchedItems.Count; i++)
            {
                dynamic update = installCollection.Item(i);
                if (!Convert.ToBoolean(update.EulaAccepted))
                {
                    update.AcceptEula();
                }
            }

            progress?.Report(new OperationProgress("WindowsUpdate", WingetProgressPhase.Downloading, 0, 0, targetUpdates.Count));

            dynamic downloader = session.CreateUpdateDownloader();
            downloader.Updates = installCollection;
            object? downloadResultObj = null;
            try
            {
                dynamic downloadResult = await RunComJobAsync(
                    callback => downloader.BeginDownload(new WindowsUpdateCompletionCallback(), callback, null),
                    job => downloader.EndDownload(job), cancellationToken).ConfigureAwait(false);
                downloadResultObj = (object)downloadResult;
                if (Convert.ToInt32(downloadResult.ResultCode) != 2)
                {
                    return WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>.Failure(
                        new WindowsUpdateError("Windows Update download did not complete successfully. Installation was not started."), string.Empty);
                }
            }
            finally
            {
                TryReleaseCom(downloadResultObj);
                TryReleaseCom((object)downloader);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (options.DownloadOnly)
            {
                // Same as Settings' "Download" without "Install": updates stay staged for a later install.
                progress?.Report(new OperationProgress("WindowsUpdate", WingetProgressPhase.Completed, 100, targetUpdates.Count, targetUpdates.Count));
                var downloaded = matchedItems
                    .Select(item => new WindowsUpdateInstallResult(item.Identity, item.Title, true, false, "2", "Downloaded; not installed."))
                    .ToList();
                return WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>.Success(downloaded, "COM Native Download Completed");
            }

            progress?.Report(new OperationProgress("WindowsUpdate", WingetProgressPhase.Installing, 0, 0, targetUpdates.Count));

            dynamic installer = session.CreateUpdateInstaller();
            installer.Updates = installCollection;
            object? installResultObj = null;
            try
            {
                dynamic installResult = await RunComJobAsync(
                    callback => installer.BeginInstall(new WindowsUpdateCompletionCallback(), callback, null),
                    job => installer.EndInstall(job), cancellationToken).ConfigureAwait(false);
                installResultObj = (object)installResult;

                bool overallRebootRequired = Convert.ToBoolean(installResult.RebootRequired);
                var results = new List<WindowsUpdateInstallResult>();

                for (int i = 0; i < matchedItems.Count; i++)
                {
                    var (title, identity) = matchedItems[i];
                    dynamic updateResult = installResult.GetUpdateResult(i);
                    int resultCode = Convert.ToInt32(updateResult.ResultCode);
                    int hResult = Convert.ToInt32(updateResult.HResult);
                    // OperationResultCode: 2 succeeded, 3 succeeded with errors, 4 failed, 5 aborted.
                    bool succeeded = resultCode == 2;
                    bool itemReboot = overallRebootRequired || Convert.ToBoolean(updateResult.RebootRequired);

                    results.Add(new WindowsUpdateInstallResult(
                        identity,
                        title,
                        succeeded,
                        itemReboot,
                        resultCode.ToString(),
                        succeeded
                            ? "Installed via Direct COM"
                            : DescribeInstallFailure(resultCode, hResult)));
                }

                progress?.Report(new OperationProgress("WindowsUpdate", WingetProgressPhase.Completed, 100, targetUpdates.Count, targetUpdates.Count));

                return WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>.Success(results, "COM Native Install Completed");
            }
            finally
            {
                TryReleaseCom(installResultObj);
                TryReleaseCom((object)installer);
            }
        }
        finally
        {
            TryReleaseCom(installCollectionObj);
            TryReleaseCom(searchResultObj);
            TryReleaseCom(searcherObj);
            TryReleaseCom(sessionObj);
        }
    }

    /// <summary>Message for a per-update install result: the result-code name plus the decoded HRESULT.</summary>
    internal static string DescribeInstallFailure(int resultCode, int hResult)
    {
        var state = resultCode switch
        {
            3 => "Installed with errors",
            4 => "Installation failed",
            5 => "Installation was aborted",
            _ => $"Installation ended with result code {resultCode}"
        };
        var detail = WindowsUpdateErrorCodes.Describe(hResult);
        return detail is null ? state + "." : $"{state}: {detail}";
    }

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "WUA automation job methods are provided by the operating system.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "WUA automation job methods are provided by the operating system.")]
    internal static Task<object> RunComJobAsync(Func<object, object> begin, Func<dynamic, object> end, CancellationToken cancellationToken) =>
        WindowsUpdateComJob.RunAsync(begin, job => end(job), job => ((dynamic)job).RequestAbort(), job =>
        {
            try
            {
                ((dynamic)job).CleanUp();
            }
            finally
            {
                TryReleaseCom(job);
            }
        }, cancellationToken);

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Windows Update COM dynamic invocation is protected by try-catch fallback.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Windows Update ProgID type instantiation.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Windows Update COM dynamic invocation is protected by try-catch fallback.")]
    internal static void TryRegisterMicrosoftUpdateService(dynamic searcher, WindowsUpdateOptions options)
    {
        if (!options.IncludeMicrosoftUpdates)
        {
            return;
        }

        object? serviceManagerObj = null;
        try
        {
            var serviceManagerType = Type.GetTypeFromProgID("Microsoft.Update.ServiceManager");
            if (serviceManagerType is null)
            {
                return;
            }

            dynamic serviceManager = Activator.CreateInstance(serviceManagerType)!;
            serviceManagerObj = (object)serviceManager;

            dynamic services = serviceManager.Services;
            int count = services.Count;
            for (int i = 0; i < count; i++)
            {
                dynamic service = services.Item(i);
                string serviceId = service.ServiceID?.ToString() ?? string.Empty;
                if (string.Equals(serviceId, MicrosoftUpdateServiceId, StringComparison.OrdinalIgnoreCase))
                {
                    searcher.ServerSelection = 3; // ssOthers
                    searcher.ServiceID = serviceId;
                    break;
                }
            }
        }
        catch
        {
            // Continue with the default Windows Update service. Optional service discovery must not block scanning/installing.
        }
        finally
        {
            TryReleaseCom(serviceManagerObj);
        }
    }

    internal static void TryReleaseCom(object? comObj)
    {
        if (comObj is not null && Marshal.IsComObject(comObj))
        {
            try
            {
                Marshal.ReleaseComObject(comObj);
            }
            catch
            {
                // Ignore COM release errors
            }
        }
    }
}
