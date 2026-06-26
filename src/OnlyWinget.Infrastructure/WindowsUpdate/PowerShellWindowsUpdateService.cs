using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OnlyWinget.Application.System;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Application.Winget;

namespace OnlyWinget.Infrastructure.WindowsUpdate;

public sealed class PowerShellWindowsUpdateService(
    IWingetCommandRunner commandRunner,
    ISystemCapabilityService capabilityService) : IWindowsUpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<WindowsUpdateOperationOutcome<WindowsUpdateItem>> ScanAsync(CancellationToken cancellationToken)
    {
        var unavailable = await GetUnavailableReasonAsync(cancellationToken).ConfigureAwait(false);
        if (unavailable is not null)
        {
            return WindowsUpdateOperationOutcome<WindowsUpdateItem>.Failure(
                new WindowsUpdateError(unavailable),
                string.Empty);
        }

        var result = await RunPowerShellAsync(ScanScript, cancellationToken).ConfigureAwait(false);
        var envelope = ReadEnvelope<WindowsUpdateItemDto>(result);
        return envelope.Succeeded
            ? WindowsUpdateOperationOutcome<WindowsUpdateItem>.Success(
                envelope.Rows.Select(row => row.ToModel()).ToArray(),
                result.StandardOutput)
            : WindowsUpdateOperationOutcome<WindowsUpdateItem>.Failure(
                new WindowsUpdateError(envelope.Error ?? "Windows Update scan failed.", result.StandardError),
                result.StandardOutput);
    }

    public async Task<WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>> InstallAsync(
        IReadOnlyList<WindowsUpdateIdentity> updates,
        CancellationToken cancellationToken)
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
        var script = InstallScript.Replace("__SELECTED_JSON__", EscapePowerShellHereString(selectedJson), StringComparison.Ordinal);
        var result = await RunPowerShellAsync(script, cancellationToken).ConfigureAwait(false);
        var envelope = ReadEnvelope<WindowsUpdateInstallResultDto>(result);
        return envelope.Succeeded
            ? WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>.Success(
                envelope.Rows.Select(row => row.ToModel()).ToArray(),
                result.StandardOutput)
            : WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>.Failure(
                new WindowsUpdateError(envelope.Error ?? "Windows Update install failed.", result.StandardError),
                result.StandardOutput);
    }

    private async Task<WingetCommandResult> RunPowerShellAsync(string script, CancellationToken cancellationToken)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        return await commandRunner.RunAsync(
                "powershell.exe",
                ["-NoProfile", "-ExecutionPolicy", "Bypass", "-EncodedCommand", encoded],
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<string?> GetUnavailableReasonAsync(CancellationToken cancellationToken)
    {
        var capabilities = await capabilityService.GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
        return capabilities.CanUseWindowsUpdate ? null : capabilities.WindowsUpdateUnavailableMessage;
    }

    private static WindowsUpdateEnvelope<T> ReadEnvelope<T>(WingetCommandResult result)
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

    private const string ScanScript = """
$ErrorActionPreference = 'Stop'
try {
    $session = New-Object -ComObject Microsoft.Update.Session
    $searcher = $session.CreateUpdateSearcher()
    $search = $searcher.Search("IsInstalled=0 and Type='Software' and IsHidden=0")
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
    $search = $searcher.Search("IsInstalled=0 and Type='Software' and IsHidden=0")
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

    private sealed record WindowsUpdateItemDto(
        [property: JsonPropertyName("updateId")] string UpdateId,
        [property: JsonPropertyName("revisionNumber")] int RevisionNumber,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("severity")] string? Severity,
        [property: JsonPropertyName("categories")] IReadOnlyList<string>? Categories,
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
