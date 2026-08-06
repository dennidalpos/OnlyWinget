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

        progress?.Report(new OperationProgress(
            "WindowsUpdate",
            WingetProgressPhase.Downloading,
            10,
            0,
            updates.Count));

        var selectedJson = JsonSerializer.Serialize(
            updates.Select(update => new WindowsUpdateIdentityDto(update.UpdateId, update.RevisionNumber)),
            JsonOptions);
        var script = ApplyOptions(InstallScript, options)
            .Replace("__SELECTED_JSON__", EscapePowerShellHereString(selectedJson), StringComparison.Ordinal);

        progress?.Report(new OperationProgress(
            "WindowsUpdate",
            WingetProgressPhase.Installing,
            50,
            0,
            updates.Count));

        var result = await RunPowerShellAsync(script, cancellationToken, global::System.TimeSpan.FromMinutes(30), requireElevation: true).ConfigureAwait(false);
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
                new WindowsUpdateError(envelope.Error ?? "Windows Update install failed.", result.StandardError),
                result.StandardOutput);
    }

    private async Task<ExternalProcessResult> RunPowerShellAsync(
        string script,
        CancellationToken cancellationToken,
        global::System.TimeSpan? timeout = null,
        bool requireElevation = false)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        return await commandRunner.RunAsync(
                "powershell.exe",
                ["-NoProfile", "-ExecutionPolicy", "Bypass", "-EncodedCommand", encoded],
                cancellationToken,
                timeout: timeout,
                requireElevation: requireElevation)
            .ConfigureAwait(false);
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
        if (!options.IncludeSoftware && !options.IncludeDrivers)
        {
            throw new ArgumentException("Select software updates, drivers, or both.", nameof(options));
        }

        var types = new List<string>();
        if (options.IncludeSoftware)
        {
            types.Add("Type='Software'");
        }

        if (options.IncludeDrivers)
        {
            types.Add("Type='Driver'");
        }

        var typeCriteria = types.Count == 2 ? string.Empty : $" and {types[0]}";
        var criteria = $"IsInstalled=0 and IsHidden=0{typeCriteria}";
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
    $search = $searcher.Search([string]$options.criteria)
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
    $search = $searcher.Search([string]$options.criteria)
    $collection = New-Object -ComObject Microsoft.Update.UpdateColl
    $metadata = @()

    foreach ($wanted in $selected) {
        foreach ($update in $search.Updates) {
            if ($update.Identity.UpdateID -ieq [string]$wanted.updateId -and [int]$update.Identity.RevisionNumber -eq [int]$wanted.revisionNumber) {
                if (-not $update.EulaAccepted) {
                    $update.AcceptEula()
                }
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

    if ($collection.Count -eq 0) {
        [pscustomobject]@{
            succeeded = $false
            rows = @()
            error = 'Selected Windows updates were not found. Run scan again.'
        } | ConvertTo-Json -Depth 8 -Compress
        exit 0
    }

    $downloader = $session.CreateUpdateDownloader()
    $downloader.Updates = $collection
    [void]$downloader.Download()

    $installer = $session.CreateUpdateInstaller()
    $installer.Updates = $collection
    $install = $installer.Install()
    $rows = @()
    for ($index = 0; $index -lt $collection.Count; $index++) {
        $updateResult = $install.GetUpdateResult($index)
        $item = $metadata[$index]
        $succeeded = $updateResult.ResultCode -eq 2 -or $updateResult.ResultCode -eq 3
        $message = if ($updateResult.HResult -eq 0) { $null } else { ('HRESULT 0x{0:X8}' -f $updateResult.HResult) }
        $rows += [pscustomobject]@{
            updateId = [string]$item.updateId
            revisionNumber = [int]$item.revisionNumber
            title = [string]$item.title
            succeeded = [bool]$succeeded
            rebootRequired = [bool]$install.RebootRequired
            resultCode = [string]$updateResult.ResultCode
            message = $message
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
        [property: JsonPropertyName("message")] string? Message)
    {
        public WindowsUpdateInstallResult ToModel() =>
            new(
                new WindowsUpdateIdentity(UpdateId, RevisionNumber),
                Title,
                Succeeded,
                RebootRequired,
                ResultCode,
                Message);
    }
}
