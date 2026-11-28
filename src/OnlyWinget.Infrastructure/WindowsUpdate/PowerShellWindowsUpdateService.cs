using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OnlyWinget.Application.System;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Application.Winget;

namespace OnlyWinget.Infrastructure.WindowsUpdate;

public sealed class PowerShellWindowsUpdateService(
    IExternalProcessRunner commandRunner,
    ISystemCapabilityService capabilityService) : IWindowsUpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<WindowsUpdateOperationOutcome<WindowsUpdateItem>> ScanAsync(
        WindowsUpdateOptions options,
        CancellationToken cancellationToken)
    {
        var unavailable = await GetUnavailableReasonAsync(cancellationToken).ConfigureAwait(false);
        if (unavailable is not null)
        {
            return WindowsUpdateOperationOutcome<WindowsUpdateItem>.Failure(
                new WindowsUpdateError(unavailable),
                string.Empty);
        }

        var script = ApplyOptions(ScanScript, options);
        var result = await RunPowerShellAsync(script, cancellationToken, global::System.TimeSpan.FromMinutes(10)).ConfigureAwait(false);
        var envelope = ReadEnvelope<WindowsUpdateItemDto>(result);
        return envelope.Succeeded
            ? WindowsUpdateOperationOutcome<WindowsUpdateItem>.Success(
                envelope.Rows.Select(row => row.ToModel()).ToArray(),
                result.StandardOutput)
            : WindowsUpdateOperationOutcome<WindowsUpdateItem>.Failure(
                new WindowsUpdateError(envelope.Error ?? "Windows Update scan failed.", result.StandardError),
                result.StandardOutput);
    }

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "WindowsUpdate DTO types are known statically.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "WindowsUpdate DTO types are known statically.")]
    public async Task<WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>> InstallAsync(
        IReadOnlyList<WindowsUpdateIdentity> updates,
        WindowsUpdateOptions options,
        CancellationToken cancellationToken,
        IProgress<OperationProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(updates);
        var unavailable = await GetUnavailableReasonAsync(cancellationToken).ConfigureAwait(false);
        if (unavailable is not null)
        {
            return WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>.Failure(
                new WindowsUpdateError(unavailable),
                string.Empty);
        }

        var selectedJson = JsonSerializer.Serialize(
            updates.Select(update => new WindowsUpdateIdentityDto(update.UpdateId, update.RevisionNumber)),
            JsonOptions);
        var script = ApplyOptions(InstallScript, options)
            .Replace("__SELECTED_JSON__", EscapePowerShellHereString(selectedJson), StringComparison.Ordinal);

        var progressLines = progress is null
            ? null
            : new InlineProgress<string>(line =>
            {
                if (WindowsUpdateProgressParser.Parse(line, updates.Count) is { } parsed)
                {
                    progress.Report(parsed);
                }
            });

        var result = await RunPowerShellAsync(script, cancellationToken, global::System.TimeSpan.FromMinutes(30), progressLines).ConfigureAwait(false);
        var envelope = ReadEnvelope<WindowsUpdateInstallResultDto>(result);

        if (envelope.Succeeded)
        {
            progress?.Report(new OperationProgress(
                "WindowsUpdate",
                WingetProgressPhase.Completed,
                100,
                updates.Count,
                updates.Count));
        }
        else
        {
            progress?.Report(new OperationProgress(
                "WindowsUpdate",
                WingetProgressPhase.Failed,
                100,
                0,
                updates.Count));
        }

        return envelope.Succeeded
            ? WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>.Success(
                envelope.Rows.Select(row => row.ToModel()).ToArray(),
                result.StandardOutput)
            : WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>.Failure(
                new WindowsUpdateError(envelope.Error ?? "Windows Update install failed.", WindowsUpdateProgressParser.StripMarkerLines(result.StandardError)),
                result.StandardOutput);
    }

    internal async Task<ExternalProcessResult> RunPowerShellAsync(
        string script,
        CancellationToken cancellationToken,
        global::System.TimeSpan? timeout = null,
        IProgress<string>? standardErrorLines = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var eventName = @"Local\OnlyWinget-WUA-" + Guid.NewGuid().ToString("N");
        using var cancellationEvent = OperatingSystem.IsWindows()
            ? new EventWaitHandle(false, EventResetMode.ManualReset, eventName)
            : new EventWaitHandle(false, EventResetMode.ManualReset);
        using var timeoutCancellation = new CancellationTokenSource(timeout ?? global::System.TimeSpan.FromMinutes(10));
        using var requestedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellation.Token);
        using var stopProcess = new CancellationTokenSource();
        using var registration = requestedCancellation.Token.Register(() =>
        {
            cancellationEvent.Set();
            stopProcess.CancelAfter(global::System.TimeSpan.FromSeconds(30));
        });
        requestedCancellation.Token.ThrowIfCancellationRequested();
        var executable = OnlyWinget.Infrastructure.System.PowerShellExecutableProvider.GetPreferredExecutable();
        var wrappedScript = PowerShellWindowsUpdateJob.Script.Replace("__CANCELLATION_EVENT__", eventName, StringComparison.Ordinal)
            + "\ntry {\n" + script + "\n} finally { $cancellationEvent.Dispose() }";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(wrappedScript));
        ExternalProcessResult result;
        try
        {
            result = await commandRunner.RunAsync(
                executable,
                ["-NoProfile", "-ExecutionPolicy", "Bypass", "-EncodedCommand", encoded],
                stopProcess.Token,
                timeout: Timeout.InfiniteTimeSpan,
                standardErrorLines: standardErrorLines)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stopProcess.IsCancellationRequested)
        {
            throw new TimeoutException("Windows Update did not finish cancellation within 30 seconds. Its native operation may still be running; do not retry automatically.");
        }

        if (result.StandardError.Contains("##OWU-ABORT-FAILED##", StringComparison.Ordinal)
            || result.StandardError.Contains("##OWU-CLEANUP-FAILED##", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Windows Update native cancellation or cleanup failed. Its operation state is uncertain; do not retry automatically. " + result.StandardError);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (timeoutCancellation.IsCancellationRequested)
        {
            throw new TimeoutException("Windows Update timed out; cancellation was requested and the worker exited.");
        }

        return result;
    }

    private async Task<string?> GetUnavailableReasonAsync(CancellationToken cancellationToken)
    {
        var capabilities = await capabilityService.GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
        return capabilities.CanUseWindowsUpdate ? null : capabilities.WindowsUpdateUnavailableMessage;
    }

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "WindowsUpdate DTO types are known statically.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "WindowsUpdate DTO types are known statically.")]
    private static WindowsUpdateEnvelope<T> ReadEnvelope<T>(ExternalProcessResult result)
    {
        if (string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return new WindowsUpdateEnvelope<T>(
                false,
                [],
                string.IsNullOrWhiteSpace(result.StandardError) ? "Windows Update returned no output." : result.StandardError.Trim());
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<WindowsUpdateEnvelope<T>>(result.StandardOutput, JsonOptions);
            return envelope ?? new WindowsUpdateEnvelope<T>(false, [], "Windows Update returned invalid output.");
        }
        catch (JsonException)
        {
            return new WindowsUpdateEnvelope<T>(false, [], result.StandardOutput.Trim());
        }
    }

    private static string EscapePowerShellHereString(string value) =>
        value.Replace("'@", "' + \"@\" + '", StringComparison.Ordinal);

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "WindowsUpdate DTO types are known statically.")]
    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "WindowsUpdate DTO types are known statically.")]
    private static string ApplyOptions(string script, WindowsUpdateOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var criteria = WindowsUpdateSearchCriteria.Build(options);
        var optionsJson = JsonSerializer.Serialize(
            new WindowsUpdateOptionsDto(
                criteria,
                options.IncludeMicrosoftUpdates),
            JsonOptions);
        return script.Replace("__OPTIONS_JSON__", EscapePowerShellHereString(optionsJson), StringComparison.Ordinal);
    }

    private const string ScanScript = """
$ErrorActionPreference = 'Stop'
try {
    $options = @'
__OPTIONS_JSON__
'@ | ConvertFrom-Json
    $session = New-Object -ComObject Microsoft.Update.Session
    $searcher = $session.CreateUpdateSearcher()
    if ([bool]$options.includeMicrosoftUpdates) {
        try {
            $serviceManager = New-Object -ComObject Microsoft.Update.ServiceManager
            $microsoftUpdate = @($serviceManager.Services) | Where-Object {
                $_.ServiceID -eq '7971f918-a847-4430-9279-4a52d1efe18d'
            } | Select-Object -First 1
            if ($null -ne $microsoftUpdate) {
                $searcher.ServerSelection = 3
                $searcher.ServiceID = [string]$microsoftUpdate.ServiceID
            }
        }
        catch {
            # Continue with the default Windows Update service. Optional service discovery must not block scanning.
        }
    }
    $search = Invoke-OnlyWingetUpdateJob -Begin { param($callback) $searcher.BeginSearch([string]$options.criteria, $callback, $null) } -End { param($job) $searcher.EndSearch($job) }
    if ([int]$search.ResultCode -ne 2) { throw 'Windows Update search did not complete successfully.' }
    $rows = @()
    foreach ($update in $search.Updates) {
        $categories = @()
        foreach ($category in $update.Categories) {
            if ($category.Name) {
                $categories += [string]$category.Name
            }
        }

        $rows += [pscustomobject]@{
            updateId = [string]$update.Identity.UpdateID
            revisionNumber = [int]$update.Identity.RevisionNumber
            title = [string]$update.Title
            description = [string]$update.Description
            severity = [string]$update.MsrcSeverity
            categories = $categories
            knowledgeBaseArticles = @($update.KBArticleIDs | ForEach-Object { [string]$_ })
            maxDownloadSize = [uint64]$update.MaxDownloadSize
            isDownloaded = [bool]$update.IsDownloaded
            rebootRequired = [bool]$update.RebootRequired
        }
    }

    [pscustomobject]@{
        succeeded = $true
        rows = $rows
        error = $null
    } | ConvertTo-Json -Depth 8 -Compress
}
catch {
    [pscustomobject]@{
        succeeded = $false
        rows = @()
        error = $_.Exception.Message
    } | ConvertTo-Json -Depth 8 -Compress
}
""";

    private const string InstallScript = """
$ErrorActionPreference = 'Stop'
try {
    $options = @'
__OPTIONS_JSON__
'@ | ConvertFrom-Json
    $selected = @'
__SELECTED_JSON__
'@ | ConvertFrom-Json
    if ($null -eq $selected) {
        $selected = @()
    }
    if ($selected -isnot [System.Array]) {
        $selected = @($selected)
    }

    $session = New-Object -ComObject Microsoft.Update.Session
    $searcher = $session.CreateUpdateSearcher()
    if ([bool]$options.includeMicrosoftUpdates) {
        try {
            $serviceManager = New-Object -ComObject Microsoft.Update.ServiceManager
            $microsoftUpdate = @($serviceManager.Services) | Where-Object {
                $_.ServiceID -eq '7971f918-a847-4430-9279-4a52d1efe18d'
            } | Select-Object -First 1
            if ($null -ne $microsoftUpdate) {
                $searcher.ServerSelection = 3
                $searcher.ServiceID = [string]$microsoftUpdate.ServiceID
            }
        }
        catch {
            # Continue with the default Windows Update service. Optional service discovery must not block scanning.
        }
    }
    $search = Invoke-OnlyWingetUpdateJob -Begin { param($callback) $searcher.BeginSearch([string]$options.criteria, $callback, $null) } -End { param($job) $searcher.EndSearch($job) }
    if ([int]$search.ResultCode -ne 2) { throw 'Windows Update search did not complete successfully. Installation was not started.' }
    $collection = New-Object -ComObject Microsoft.Update.UpdateColl
    $metadata = @()

    foreach ($wanted in $selected) {
        foreach ($update in $search.Updates) {
            if ($update.Identity.UpdateID -ieq [string]$wanted.updateId -and [int]$update.Identity.RevisionNumber -eq [int]$wanted.revisionNumber) {
                [void]$collection.Add($update)
                $metadata += [pscustomobject]@{
                    updateId = [string]$update.Identity.UpdateID
                    revisionNumber = [int]$update.Identity.RevisionNumber
                    title = [string]$update.Title
                }
                break
            }
        }
    }

    if ($collection.Count -eq 0 -or $collection.Count -ne $selected.Count) {
        [pscustomobject]@{
            succeeded = $false
            rows = @()
            error = 'Selected Windows updates were not found. Run scan again.'
        } | ConvertTo-Json -Depth 8 -Compress
        exit 0
    }

    foreach ($update in $collection) {
        if (-not $update.EulaAccepted) { $update.AcceptEula() }
    }

    [Console]::Error.WriteLine("##OWU-PROGRESS##Downloading##0")
    $downloader = $session.CreateUpdateDownloader()
    $downloader.Updates = $collection
    $downloadResult = Invoke-OnlyWingetUpdateJob -Begin { param($callback, $progressCallback) $downloader.BeginDownload($progressCallback, $callback, $null) } -End { param($job) $downloader.EndDownload($job) }
    if ([int]$downloadResult.ResultCode -ne 2) {
        throw 'Windows Update download did not complete successfully. Installation was not started.'
    }
    [Console]::Error.WriteLine("##OWU-PROGRESS##Downloading##100")

    [Console]::Error.WriteLine("##OWU-PROGRESS##Installing##0")
    $installer = $session.CreateUpdateInstaller()
    $installer.Updates = $collection
    $install = Invoke-OnlyWingetUpdateJob -Begin { param($callback, $progressCallback) $installer.BeginInstall($progressCallback, $callback, $null) } -End { param($job) $installer.EndInstall($job) }
    [Console]::Error.WriteLine("##OWU-PROGRESS##Installing##100")
    $rows = @()
    for ($index = 0; $index -lt $collection.Count; $index++) {
        $updateResult = $install.GetUpdateResult($index)
        $item = $metadata[$index]
        $succeeded = $updateResult.ResultCode -eq 2
        $rows += [pscustomobject]@{
            updateId = [string]$item.updateId
            revisionNumber = [int]$item.revisionNumber
            title = [string]$item.title
            succeeded = [bool]$succeeded
            rebootRequired = [bool]$install.RebootRequired
            resultCode = [string]$updateResult.ResultCode
            hResult = [int]$updateResult.HResult
        }
    }

    [pscustomobject]@{
        succeeded = $true
        rows = $rows
        error = $null
    } | ConvertTo-Json -Depth 8 -Compress
}
catch {
    [pscustomobject]@{
        succeeded = $false
        rows = @()
        error = $_.Exception.Message
    } | ConvertTo-Json -Depth 8 -Compress
}
""";

    private sealed record WindowsUpdateEnvelope<T>(
        [property: JsonPropertyName("succeeded")] bool Succeeded,
        [property: JsonPropertyName("rows")] IReadOnlyList<T> Rows,
        [property: JsonPropertyName("error")] string? Error);

    private sealed record WindowsUpdateIdentityDto(
        [property: JsonPropertyName("updateId")] string UpdateId,
        [property: JsonPropertyName("revisionNumber")] int RevisionNumber);

    private sealed record WindowsUpdateOptionsDto(
        [property: JsonPropertyName("criteria")] string Criteria,
        [property: JsonPropertyName("includeMicrosoftUpdates")] bool IncludeMicrosoftUpdates);

    private sealed record WindowsUpdateItemDto(
        [property: JsonPropertyName("updateId")] string UpdateId,
        [property: JsonPropertyName("revisionNumber")] int RevisionNumber,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("severity")] string? Severity,
        [property: JsonPropertyName("categories")] IReadOnlyList<string>? Categories,
        [property: JsonPropertyName("knowledgeBaseArticles")] IReadOnlyList<string>? KnowledgeBaseArticles,
        [property: JsonPropertyName("maxDownloadSize")] ulong MaxDownloadSize,
        [property: JsonPropertyName("isDownloaded")] bool IsDownloaded,
        [property: JsonPropertyName("rebootRequired")] bool RebootRequired)
    {
        public WindowsUpdateItem ToModel() =>
            new(
                new WindowsUpdateIdentity(UpdateId, RevisionNumber),
                Title,
                Description,
                Severity,
                Categories ?? [],
                KnowledgeBaseArticles ?? [],
                MaxDownloadSize,
                IsDownloaded,
                RebootRequired);
    }

    private sealed record WindowsUpdateInstallResultDto(
        [property: JsonPropertyName("updateId")] string UpdateId,
        [property: JsonPropertyName("revisionNumber")] int RevisionNumber,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("succeeded")] bool Succeeded,
        [property: JsonPropertyName("rebootRequired")] bool RebootRequired,
        [property: JsonPropertyName("resultCode")] string ResultCode,
        [property: JsonPropertyName("hResult")] int HResult)
    {
        public WindowsUpdateInstallResult ToModel() =>
            new(
                new WindowsUpdateIdentity(UpdateId, RevisionNumber),
                Title,
                Succeeded,
                RebootRequired,
                ResultCode,
                WindowsUpdateErrorCodes.Describe(HResult));
    }
}
