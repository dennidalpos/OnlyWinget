using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Application.Winget;

namespace OnlyWinget.Infrastructure.WindowsUpdate;

/// <summary>History, hide/show and uninstall through the Windows Update Agent COM API.</summary>
[SupportedOSPlatform("windows")]
public sealed partial class ComWindowsUpdateManagementService(
    ILogger<ComWindowsUpdateManagementService>? logger = null) : IWindowsUpdateManagementService
{
    private const string ProgId = "Microsoft.Update.Session";

    public Task<WindowsUpdateOperationOutcome<WindowsUpdateHistoryEntry>> GetHistoryAsync(
        int maxEntries,
        CancellationToken cancellationToken) =>
        RunAsync(() => ReadHistory(Math.Clamp(maxEntries, 1, 2000), cancellationToken),
            ex => WindowsUpdateOperationOutcome<WindowsUpdateHistoryEntry>.Failure(ToError(ex), string.Empty),
            cancellationToken);

    public Task<WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>> SetHiddenAsync(
        IReadOnlyList<WindowsUpdateIdentity> updates,
        bool hidden,
        WindowsUpdateOptions options,
        CancellationToken cancellationToken) =>
        RunAsync(() => ChangeHidden(updates, hidden, options, cancellationToken),
            ex => WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>.Failure(ToError(ex), string.Empty),
            cancellationToken);

    public Task<WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>> UninstallAsync(
        IReadOnlyList<WindowsUpdateIdentity> updates,
        CancellationToken cancellationToken,
        IProgress<OperationProgress>? progress = null) =>
        RunAsync(() => UninstallCore(updates, progress, cancellationToken).GetAwaiter().GetResult(),
            ex => WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>.Failure(ToError(ex), string.Empty),
            cancellationToken);

    private async Task<T> RunAsync<T>(Func<T> work, Func<Exception, T> onError, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return await Task.Run(work, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            logger?.LogWarning(ex, "Windows Update management operation failed.");
            return onError(ex);
        }
    }

    private static WindowsUpdateError ToError(Exception ex) =>
        new(ex is COMException com ? WindowsUpdateErrorCodes.Describe(com.HResult) ?? ex.Message : ex.Message);

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "WUA automation metadata is supplied by the operating system.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "WUA automation requires the runtime binder.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Windows Update ProgID type instantiation.")]
    private static WindowsUpdateOperationOutcome<WindowsUpdateHistoryEntry> ReadHistory(int maxEntries, CancellationToken cancellationToken)
    {
        var sessionType = Type.GetTypeFromProgID(ProgId)
            ?? throw new InvalidOperationException("Windows Update Agent is not available.");
        object? sessionObj = null;
        object? searcherObj = null;
        object? historyObj = null;
        try
        {
            dynamic session = Activator.CreateInstance(sessionType)!;
            sessionObj = (object)session;
            dynamic searcher = session.CreateUpdateSearcher();
            searcherObj = (object)searcher;

            int total = searcher.GetTotalHistoryCount();
            var count = Math.Min(total, maxEntries);
            var rows = new List<WindowsUpdateHistoryEntry>(count);
            if (count == 0)
            {
                return WindowsUpdateOperationOutcome<WindowsUpdateHistoryEntry>.Success(rows, "No history");
            }

            dynamic history = searcher.QueryHistory(0, count);
            historyObj = (object)history;
            int available = history.Count;
            for (var index = 0; index < available; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                object? entryObj = null;
                try
                {
                    dynamic entry = history.Item(index);
                    entryObj = (object)entry;
                    rows.Add(MapHistoryEntry(entry));
                }
                catch (Exception ex) when (ex is COMException or InvalidCastException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
                {
                    // WUA occasionally returns a null/blank slot; skip it rather than failing the whole history.
                }
                finally
                {
                    ComWindowsUpdateService.TryReleaseCom(entryObj);
                }
            }

            return WindowsUpdateOperationOutcome<WindowsUpdateHistoryEntry>.Success(
                rows.OrderByDescending(row => row.Date).ToList(), $"History: {rows.Count} of {total}");
        }
        finally
        {
            ComWindowsUpdateService.TryReleaseCom(historyObj);
            ComWindowsUpdateService.TryReleaseCom(searcherObj);
            ComWindowsUpdateService.TryReleaseCom(sessionObj);
        }
    }

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "WUA automation metadata is supplied by the operating system.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "WUA automation requires the runtime binder.")]
    private static WindowsUpdateHistoryEntry MapHistoryEntry(dynamic entry)
    {
        string title = entry.Title ?? string.Empty;
        DateTime date = entry.Date;
        int operation = Convert.ToInt32(entry.Operation);
        int resultCode = Convert.ToInt32(entry.ResultCode);
        int hResult = Convert.ToInt32(entry.HResult);

        string? updateId = null;
        object? identityObj = null;
        try
        {
            dynamic identity = entry.UpdateIdentity;
            identityObj = (object?)identity;
            updateId = identity?.UpdateID?.ToString();
        }
        catch (Exception ex) when (ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            // Not every history row carries an identity.
        }
        finally
        {
            ComWindowsUpdateService.TryReleaseCom(identityObj);
        }

        // History categories are Product-type with empty IDs on real machines, so the title decides the group.
        return new WindowsUpdateHistoryEntry(
            new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Utc)),
            title,
            operation switch
            {
                1 => WindowsUpdateHistoryOperation.Install,
                2 => WindowsUpdateHistoryOperation.Uninstall,
                _ => WindowsUpdateHistoryOperation.Other
            },
            ToHistoryResult(resultCode),
            hResult,
            WindowsUpdateClassifier.FromTitle(title),
            ExtractKnowledgeBase(title),
            updateId);
    }

    internal static WindowsUpdateHistoryResult ToHistoryResult(int resultCode) => resultCode switch
    {
        0 => WindowsUpdateHistoryResult.NotStarted,
        1 => WindowsUpdateHistoryResult.InProgress,
        2 => WindowsUpdateHistoryResult.Succeeded,
        3 => WindowsUpdateHistoryResult.SucceededWithErrors,
        5 => WindowsUpdateHistoryResult.Aborted,
        _ => WindowsUpdateHistoryResult.Failed
    };

    internal static string? ExtractKnowledgeBase(string title)
    {
        var match = KnowledgeBasePattern().Match(title);
        return match.Success ? "KB" + match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"\bKB\s?(\d{6,8})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex KnowledgeBasePattern();

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "WUA automation metadata is supplied by the operating system.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "WUA automation requires the runtime binder.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Windows Update ProgID type instantiation.")]
    private WindowsUpdateOperationOutcome<WindowsUpdateInstallResult> ChangeHidden(
        IReadOnlyList<WindowsUpdateIdentity> targets,
        bool hidden,
        WindowsUpdateOptions options,
        CancellationToken cancellationToken)
    {
        var sessionType = Type.GetTypeFromProgID(ProgId)
            ?? throw new InvalidOperationException("Windows Update Agent is not available.");
        object? sessionObj = null;
        object? searcherObj = null;
        object? resultObj = null;
        try
        {
            dynamic session = Activator.CreateInstance(sessionType)!;
            sessionObj = (object)session;
            dynamic searcher = session.CreateUpdateSearcher();
            searcherObj = (object)searcher;
            ComWindowsUpdateService.TryRegisterMicrosoftUpdateService(searcher, options);

            // Hiding applies to what the user currently sees (not hidden); unhiding to the hidden list.
            var criteria = WindowsUpdateSearchCriteria.Build(options with { ShowHidden = !hidden, IncludeOptionalUpdates = true, IncludeSoftware = true, IncludeDrivers = true });
            dynamic searchResult = searcher.Search(criteria);
            resultObj = (object)searchResult;
            dynamic found = searchResult.Updates;

            var wanted = targets.ToDictionary(t => $"{t.UpdateId}|{t.RevisionNumber}", StringComparer.OrdinalIgnoreCase);
            var results = new List<WindowsUpdateInstallResult>();
            int count = found.Count;
            for (var index = 0; index < count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                dynamic update = found.Item(index);
                dynamic identity = update.Identity;
                var key = $"{identity.UpdateID}|{Convert.ToInt32(identity.RevisionNumber)}";
                if (!wanted.TryGetValue(key, out var target))
                {
                    continue;
                }

                string title = update.Title ?? target.UpdateId;
                try
                {
                    update.IsHidden = hidden;
                    results.Add(new WindowsUpdateInstallResult(target, title, true, false, "2", hidden ? "Hidden." : "Shown again."));
                }
                catch (COMException ex)
                {
                    results.Add(new WindowsUpdateInstallResult(target, title, false, false, "4", WindowsUpdateErrorCodes.Describe(ex.HResult) ?? ex.Message));
                }
            }

            foreach (var missing in wanted.Values.Where(w => results.All(r => r.Identity != w)))
            {
                results.Add(new WindowsUpdateInstallResult(missing, missing.UpdateId, false, false, "4", "The update was not found. Scan again."));
            }

            return WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>.Success(results, hidden ? "Hide completed" : "Unhide completed");
        }
        finally
        {
            ComWindowsUpdateService.TryReleaseCom(resultObj);
            ComWindowsUpdateService.TryReleaseCom(searcherObj);
            ComWindowsUpdateService.TryReleaseCom(sessionObj);
        }
    }

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "WUA automation metadata is supplied by the operating system.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Windows Update ProgID type instantiation.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "WUA automation requires the runtime binder.")]
    private async Task<WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>> UninstallCore(
        IReadOnlyList<WindowsUpdateIdentity> targets,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var sessionType = Type.GetTypeFromProgID(ProgId)
            ?? throw new InvalidOperationException("Windows Update Agent is not available.");
        object? sessionObj = null;
        object? searcherObj = null;
        object? searchResultObj = null;
        object? collectionObj = null;
        try
        {
            dynamic session = Activator.CreateInstance(sessionType)!;
            sessionObj = (object)session;
            dynamic searcher = session.CreateUpdateSearcher();
            searcherObj = (object)searcher;

            dynamic searchResult = searcher.Search("IsInstalled=1 and IsHidden=0");
            searchResultObj = (object)searchResult;
            dynamic installed = searchResult.Updates;

            // History does not record the installed revision, so installed updates are matched by UpdateID.
            var wanted = targets.DistinctBy(t => t.UpdateId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(t => t.UpdateId, StringComparer.OrdinalIgnoreCase);
            var collectionType = Type.GetTypeFromProgID("Microsoft.Update.UpdateColl")
                ?? throw new InvalidOperationException("Windows Update collection is not available.");
            dynamic collection = Activator.CreateInstance(collectionType)!;
            collectionObj = (object)collection;

            var results = new List<WindowsUpdateInstallResult>();
            var matched = new List<(string Title, WindowsUpdateIdentity Identity)>();
            int count = installed.Count;
            for (var index = 0; index < count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                dynamic update = installed.Item(index);
                dynamic identity = update.Identity;
                string key = identity.UpdateID?.ToString() ?? string.Empty;
                if (!wanted.TryGetValue(key, out var target))
                {
                    continue;
                }

                string title = update.Title ?? target.UpdateId;
                if (!Convert.ToBoolean(update.IsUninstallable))
                {
                    results.Add(new WindowsUpdateInstallResult(target, title, false, false, "4", "Windows does not allow this update to be uninstalled."));
                    continue;
                }

                collection.Add(update);
                matched.Add((title, target));
            }

            foreach (var missing in wanted.Values.Where(w => results.All(r => r.Identity != w) && matched.All(m => m.Identity != w)))
            {
                results.Add(new WindowsUpdateInstallResult(missing, missing.UpdateId, false, false, "4", "The update is not installed or was not found."));
            }

            if (matched.Count == 0)
            {
                return WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>.Success(results, "Nothing to uninstall");
            }

            progress?.Report(new OperationProgress("WindowsUpdate", WingetProgressPhase.Installing, 0, 0, matched.Count));
            dynamic installer = session.CreateUpdateInstaller();
            installer.Updates = collection;
            installer.ForceQuiet = true;
            object? uninstallResultObj = null;
            try
            {
                dynamic uninstallResult = await ComWindowsUpdateService.RunComJobAsync(
                    callback => installer.BeginUninstall(new WindowsUpdateCompletionCallback(), callback, null),
                    job => installer.EndUninstall(job), cancellationToken).ConfigureAwait(false);
                uninstallResultObj = (object)uninstallResult;
                var overallReboot = Convert.ToBoolean(uninstallResult.RebootRequired);
                for (var index = 0; index < matched.Count; index++)
                {
                    var (title, identity) = matched[index];
                    dynamic itemResult = uninstallResult.GetUpdateResult(index);
                    var resultCode = Convert.ToInt32(itemResult.ResultCode);
                    var hResult = Convert.ToInt32(itemResult.HResult);
                    var ok = resultCode == 2;
                    results.Add(new WindowsUpdateInstallResult(
                        identity, title, ok, overallReboot || Convert.ToBoolean(itemResult.RebootRequired), resultCode.ToString(),
                        ok ? "Uninstalled." : ComWindowsUpdateService.DescribeInstallFailure(resultCode, hResult).Replace("Installation", "Uninstallation", StringComparison.Ordinal)));
                }

                progress?.Report(new OperationProgress("WindowsUpdate", WingetProgressPhase.Completed, 100, matched.Count, matched.Count));
                return WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>.Success(results, "COM Native Uninstall Completed");
            }
            finally
            {
                ComWindowsUpdateService.TryReleaseCom(uninstallResultObj);
                ComWindowsUpdateService.TryReleaseCom((object)installer);
            }
        }
        finally
        {
            ComWindowsUpdateService.TryReleaseCom(collectionObj);
            ComWindowsUpdateService.TryReleaseCom(searchResultObj);
            ComWindowsUpdateService.TryReleaseCom(searcherObj);
            ComWindowsUpdateService.TryReleaseCom(sessionObj);
        }
    }
}
