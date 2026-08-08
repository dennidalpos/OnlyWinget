using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using OnlyWinget.Application.System;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Application.Winget;

namespace OnlyWinget.Infrastructure.WindowsUpdate;

[SupportedOSPlatform("windows")]
public sealed class ComWindowsUpdateService(
    PowerShellWindowsUpdateService fallbackService,
    ILogger<ComWindowsUpdateService>? logger = null) : IWindowsUpdateService
{
    private const string ProgId = "Microsoft.Update.Session";

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

            var query = options.IncludeDrivers
                ? "IsInstalled=0"
                : "IsInstalled=0 and Type='Software'";

            dynamic searchResult = searcher.Search(query);
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

            dynamic searchResult = searcher.Search("IsInstalled=0");
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
                    installCollection.Add(update);
                    matchedItems.Add((update.Title?.ToString() ?? updateId, targetIdent));
                }
            }

            if (matchedItems.Count == 0)
            {
                return WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>.Success([], "No matching COM updates found to install");
            }

            progress?.Report(new OperationProgress("WindowsUpdate", WingetProgressPhase.Downloading, 10, 0, targetUpdates.Count));

            dynamic downloader = session.CreateUpdateDownloader();
            downloader.Updates = installCollection;
            downloader.Download();

            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(new OperationProgress("WindowsUpdate", WingetProgressPhase.Installing, 50, 0, targetUpdates.Count));

            dynamic installer = session.CreateUpdateInstaller();
            installer.Updates = installCollection;
            dynamic installResult = installer.Install();

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
