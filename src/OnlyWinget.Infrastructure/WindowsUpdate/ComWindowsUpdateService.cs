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

        return $"IsInstalled=0 and IsHidden=0{typeCriteria}";
    }
}

[SupportedOSPlatform("windows")]
public sealed class ComWindowsUpdateService(
    PowerShellWindowsUpdateService fallbackService,
    ILogger<ComWindowsUpdateService>? logger = null) : IWindowsUpdateService
{
    private const string ProgId = "Microsoft.Update.Session";
    private const string MicrosoftUpdateServiceId = "7971f918-a847-4430-9279-4a52d1efe18d";

    public async Task<WindowsUpdateOperationOutcome<WindowsUpdateItem>> ScanAsync(
        WindowsUpdateOptions options,
        CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var sessionType = Type.GetTypeFromProgID(ProgId);
                if (sessionType is not null)
                {
                    var outcome = await Task.Run(() => ScanNativeCom(options), cancellationToken).ConfigureAwait(false);
                    if (outcome is not null && outcome.Succeeded)
                    {
                        logger?.LogInformation("Windows Update COM scan completed successfully via Microsoft.Update.Session with {Count} updates.", outcome.Rows.Count);
                        return outcome;
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Windows Update COM Interop failed. Falling back to PowerShell execution.");
            }
        }

        return await fallbackService.ScanAsync(options, cancellationToken).ConfigureAwait(false);
    }

    public async Task<WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>> InstallAsync(
        IReadOnlyList<WindowsUpdateIdentity> updates,
        WindowsUpdateOptions options,
        CancellationToken cancellationToken,
        IProgress<OperationProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(updates);

        if (OperatingSystem.IsWindows() && updates.Count > 0)
        {
            try
            {
                var sessionType = Type.GetTypeFromProgID(ProgId);
                if (sessionType is not null)
                {
                    var outcome = await Task.Run(() => InstallNativeCom(updates, options, progress, cancellationToken), cancellationToken).ConfigureAwait(false);
                    if (outcome is not null && outcome.Succeeded)
                    {
                        logger?.LogInformation("Windows Update COM installation completed successfully for {Count} updates.", outcome.Rows.Count);
                        return outcome;
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Windows Update COM install failed. Falling back to PowerShell execution.");
            }
        }

        return await fallbackService.InstallAsync(updates, options, cancellationToken, progress).ConfigureAwait(false);
    }

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Windows Update COM dynamic invocation is protected by try-catch fallback.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Windows Update ProgID type instantiation.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Windows Update COM dynamic invocation is protected by try-catch fallback.")]
    private static WindowsUpdateOperationOutcome<WindowsUpdateItem>? ScanNativeCom(WindowsUpdateOptions options)
    {
        var sessionType = Type.GetTypeFromProgID(ProgId);
        if (sessionType is null) return null;

        object? sessionObj = null;
        object? searcherObj = null;
        object? searchResultObj = null;

        try
        {
            dynamic session = Activator.CreateInstance(sessionType)!;
            sessionObj = (object)session;

            dynamic searcher = session.CreateUpdateSearcher();
            searcherObj = (object)searcher;
            TryRegisterMicrosoftUpdateService(searcher, options);

            dynamic searchResult = searcher.Search(WindowsUpdateSearchCriteria.Build(options));
            searchResultObj = (object)searchResult;

            dynamic updateCollection = searchResult.Updates;

            var items = new List<WindowsUpdateItem>();
            int count = updateCollection.Count;

            for (int i = 0; i < count; i++)
            {
                dynamic update = updateCollection.Item(i);
                dynamic identity = update.Identity;

                string updateId = identity.UpdateID?.ToString() ?? Guid.NewGuid().ToString();
                int revisionNumber = (int)(identity.RevisionNumber ?? 1);
                string title = update.Title?.ToString() ?? "Windows Update";
                string? description = update.Description?.ToString();

                var categories = new List<string>();
                try
                {
                    dynamic categoryColl = update.Categories;
                    int catCount = categoryColl.Count;
                    for (int c = 0; c < catCount; c++)
                    {
                        string? catName = categoryColl.Item(c).Name?.ToString();
                        if (!string.IsNullOrWhiteSpace(catName)) categories.Add(catName);
                    }
                    TryReleaseCom((object)categoryColl);
                }
                catch { }

                var kbArticles = new List<string>();
                try
                {
                    dynamic kbColl = update.KBArticleIDs;
                    int kbCount = kbColl.Count;
                    for (int k = 0; k < kbCount; k++)
                    {
                        string? kb = kbColl.Item(k)?.ToString();
                        if (!string.IsNullOrWhiteSpace(kb)) kbArticles.Add($"KB{kb}");
                    }
                    TryReleaseCom((object)kbColl);
                }
                catch { }

                ulong maxDownloadSize = 0;
                try { maxDownloadSize = Convert.ToUInt64(update.MaxDownloadSize); } catch { }

                bool isDownloaded = false;
                try { isDownloaded = Convert.ToBoolean(update.IsDownloaded); } catch { }

                bool rebootRequired = false;
                try
                {
                    int behavior = Convert.ToInt32(update.InstallationBehavior.RebootBehavior);
                    rebootRequired = behavior != 0;
                }
                catch { }

                items.Add(new WindowsUpdateItem(
                    new WindowsUpdateIdentity(updateId, revisionNumber),
                    title,
                    description,
                    "Important",
                    categories,
                    kbArticles,
                    maxDownloadSize,
                    isDownloaded,
                    rebootRequired));

                TryReleaseCom((object)update);
            }

            return WindowsUpdateOperationOutcome<WindowsUpdateItem>.Success(items, "COM Native Search Completed");
        }
        finally
        {
            TryReleaseCom(searchResultObj);
            TryReleaseCom(searcherObj);
            TryReleaseCom(sessionObj);
        }
    }

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Windows Update COM dynamic invocation is protected by try-catch fallback.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Windows Update ProgID type instantiation.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Windows Update COM dynamic invocation is protected by try-catch fallback.")]
    private static WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>? InstallNativeCom(
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

            dynamic searchResult = searcher.Search(WindowsUpdateSearchCriteria.Build(options));
            searchResultObj = (object)searchResult;

            dynamic availableUpdates = searchResult.Updates;
            int availableCount = availableUpdates.Count;

            var targetMap = targetUpdates.ToDictionary(u => u.UpdateId, StringComparer.OrdinalIgnoreCase);
            var updateCollectionType = Type.GetTypeFromProgID("Microsoft.Update.UpdateColl");
            dynamic installCollection = Activator.CreateInstance(updateCollectionType ?? Type.GetTypeFromCLSID(new Guid("1361661A-2A21-4226-928E-2E31A2F69527"))!)!;
            installCollectionObj = (object)installCollection;

            var matchedItems = new List<(string Title, WindowsUpdateIdentity Identity)>();

            for (int i = 0; i < availableCount; i++)
            {
                dynamic update = availableUpdates.Item(i);
                dynamic identity = update.Identity;
                string updateId = identity.UpdateID?.ToString() ?? string.Empty;

                if (targetMap.TryGetValue(updateId, out var targetIdent))
                {
                    bool eulaAccepted = false;
                    try { eulaAccepted = Convert.ToBoolean(update.EulaAccepted); } catch { }
                    if (!eulaAccepted)
                    {
                        try { update.AcceptEula(); } catch { }
                    }

                    installCollection.Add(update);
                    matchedItems.Add((update.Title?.ToString() ?? updateId, targetIdent));
                }
            }

            if (matchedItems.Count == 0)
            {
                return WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>.Success([], "No matching COM updates found to install");
            }

            progress?.Report(new OperationProgress("WindowsUpdate", WingetProgressPhase.Downloading, 0, 0, targetUpdates.Count));

            dynamic downloader = session.CreateUpdateDownloader();
            downloader.Updates = installCollection;
            dynamic downloadJob = downloader.BeginDownload(null, null, null);
            object? downloadJobObj = (object)downloadJob;
            try
            {
                while (!(bool)downloadJob.IsCompleted)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new OperationProgress("WindowsUpdate", WingetProgressPhase.Downloading, ReadPercentComplete(downloadJob), 0, targetUpdates.Count));
                    Thread.Sleep(500);
                }
                downloader.EndDownload(downloadJob);
            }
            finally
            {
                TryReleaseCom(downloadJobObj);
            }

            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(new OperationProgress("WindowsUpdate", WingetProgressPhase.Installing, 0, 0, targetUpdates.Count));

            dynamic installer = session.CreateUpdateInstaller();
            installer.Updates = installCollection;
            dynamic installJob = installer.BeginInstall(null, null, null);
            object? installJobObj = (object)installJob;
            dynamic installResult;
            try
            {
                while (!(bool)installJob.IsCompleted)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new OperationProgress("WindowsUpdate", WingetProgressPhase.Installing, ReadPercentComplete(installJob), 0, targetUpdates.Count));
                    Thread.Sleep(500);
                }
                installResult = installer.EndInstall(installJob);
            }
            finally
            {
                TryReleaseCom(installJobObj);
            }

            int resultCode = Convert.ToInt32(installResult.ResultCode);
            bool rebootRequired = Convert.ToBoolean(installResult.RebootRequired);
            bool succeeded = resultCode is 2 or 3;

            var results = matchedItems.Select(m => new WindowsUpdateInstallResult(
                m.Identity,
                m.Title,
                succeeded,
                rebootRequired,
                resultCode.ToString(),
                succeeded ? "Installed via Direct COM" : "COM installation completed with result code " + resultCode
            )).ToList();

            progress?.Report(new OperationProgress("WindowsUpdate", WingetProgressPhase.Completed, 100, targetUpdates.Count, targetUpdates.Count));

            return WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>.Success(results, "COM Native Install Completed");
        }
        finally
        {
            TryReleaseCom(installCollectionObj);
            TryReleaseCom(searchResultObj);
            TryReleaseCom(searcherObj);
            TryReleaseCom(sessionObj);
        }
    }

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Windows Update COM dynamic invocation is protected by try-catch fallback.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Windows Update ProgID type instantiation.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Windows Update COM dynamic invocation is protected by try-catch fallback.")]
    private static void TryRegisterMicrosoftUpdateService(dynamic searcher, WindowsUpdateOptions options)
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

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Windows Update COM dynamic invocation is protected by try-catch fallback.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Windows Update COM dynamic invocation is protected by try-catch fallback.")]
    private static int ReadPercentComplete(dynamic job)
    {
        try
        {
            return Math.Clamp(Convert.ToInt32(job.GetProgress().PercentComplete), 0, 100);
        }
        catch
        {
            return 0;
        }
    }

    private static void TryReleaseCom(object? comObj)
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
