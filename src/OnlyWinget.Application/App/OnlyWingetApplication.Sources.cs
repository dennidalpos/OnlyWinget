using OnlyWinget.Application.Activity;
using OnlyWinget.Application.Storage;
using OnlyWinget.Application.Winget;

namespace OnlyWinget.Application.App;

public sealed partial class OnlyWingetApplication
{
    public async Task<ApplicationActionResult> RefreshSourcesAsync(CancellationToken callerCancellationToken)
    {
        return await RunAsync(
                ApplicationBusyState.ManagingSources,
                callerCancellationToken,
                async cancellationToken =>
                {
                    RequireWinget();
                    await EnsureOfficialSourcesConfiguredAsync(cancellationToken).ConfigureAwait(false);
                    var outcome = await sourceService.ListSourcesAsync(cancellationToken).ConfigureAwait(false);
                    ApplySourceOutcome(outcome, updateRows: true);
                    AddActivity(ActivitySeverity.Information, "Sources refreshed", $"{ReadState(() => sources.Count)} source(s).");
                },
                "Unable to refresh winget sources.")
            .ConfigureAwait(false);
    }

    public async Task<ApplicationActionResult> UpdateSourcesAsync(CancellationToken cancellationToken)
    {
        return await RunSourceMutationAsync(
                token => sourceService.UpdateSourcesAsync(token),
                "Sources updated",
                "winget source update completed.",
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ApplicationActionResult> AddSourceAsync(
        string name,
        string argument,
        CancellationToken cancellationToken)
    {
        return await RunSourceMutationAsync(
                token => sourceService.AddSourceAsync(name, argument, token),
                "Source added",
                name,
                cancellationToken,
                requiresElevation: true)
            .ConfigureAwait(false);
    }

    public async Task<ApplicationActionResult> RemoveSourceAsync(string name, CancellationToken cancellationToken)
    {
        return await RunSourceMutationAsync(
                token => sourceService.RemoveSourceAsync(name, token),
                "Source removed",
                name,
                cancellationToken,
                requiresElevation: true)
            .ConfigureAwait(false);
    }

    public async Task<ApplicationActionResult> ResetSourcesAsync(CancellationToken cancellationToken)
    {
        return await RunSourceMutationAsync(
                token => sourceService.ResetSourcesAsync(token),
                "Sources reset",
                "winget sources reset to defaults.",
                cancellationToken,
                requiresElevation: true,
                resetPreferences: true)
            .ConfigureAwait(false);
    }

    public async Task<ApplicationActionResult> SetSourceEnabledAsync(
        string name,
        bool isEnabled,
        CancellationToken callerCancellationToken)
    {
        return await RunAsync(
                ApplicationBusyState.ManagingSources,
                callerCancellationToken,
                async cancellationToken =>
                {
                    RequireWinget();
                    if (!ReadState(() => sources.Any(source => string.Equals(source.Name, name, StringComparison.OrdinalIgnoreCase))))
                    {
                        throw new InvalidOperationException("The winget source was not found.");
                    }

                    var updatedDisabledSources = ReadState(() => new HashSet<string>(disabledSources, StringComparer.OrdinalIgnoreCase));
                    if (isEnabled)
                    {
                        updatedDisabledSources.Remove(name);
                    }
                    else
                    {
                        updatedDisabledSources.Add(name);
                    }

                    await sourcePreferences.SaveAsync(
                            new SourcePreferences(updatedDisabledSources.ToArray(), ReadState(() => defaultSourcesConfigured)),
                            cancellationToken)
                        .ConfigureAwait(false);
                    UpdateState(() =>
                    {
                        disabledSources.Clear();
                        disabledSources.UnionWith(updatedDisabledSources);
                        ApplySourcePreferences();
                    });
                    AddActivity(ActivitySeverity.Information, "Source preference changed", $"{name}: {(isEnabled ? "enabled" : "disabled")}");
                },
                "Unable to save the source preference.")
            .ConfigureAwait(false);
    }

    private async Task<ApplicationActionResult> RunSourceMutationAsync(
        Func<CancellationToken, Task<WingetOperationOutcome<WingetSource>>> operation,
        string title,
        string message,
        CancellationToken callerCancellationToken,
        bool requiresElevation = false,
        bool resetPreferences = false)
    {
        return await RunAsync(
                ApplicationBusyState.ManagingSources,
                callerCancellationToken,
                async cancellationToken =>
                {
                    RequireWinget();
                    if (requiresElevation) RequireSourceElevation();
                    cancellationToken.ThrowIfCancellationRequested();
                    var outcome = await operation(cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    ApplySourceOutcome(outcome, updateRows: false);

                    var refresh = await sourceService.ListSourcesAsync(cancellationToken).ConfigureAwait(false);
                    ApplySourceOutcome(refresh, updateRows: true);
                    var preferences = resetPreferences
                        ? new SourcePreferences([], DefaultSourcesConfigured: true)
                        : ReadState(() => new SourcePreferences(disabledSources.ToArray(), defaultSourcesConfigured));
                    try
                    {
                        await sourcePreferences.SaveAsync(preferences, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    {
                        throw new InvalidOperationException($"Sources changed, but preferences could not be saved. {exception.Message}", exception);
                    }
                    UpdateState(() =>
                    {
                        disabledSources.Clear();
                        disabledSources.UnionWith(preferences.DisabledSources);
                        defaultSourcesConfigured = preferences.DefaultSourcesConfigured;
                        ApplySourcePreferences();
                    });
                    AddActivity(ActivitySeverity.Success, title, message);
                },
                "Unable to manage winget sources.")
            .ConfigureAwait(false);
    }

    private void ApplySourceOutcome(WingetOperationOutcome<WingetSource> outcome, bool updateRows)
    {
        if (!outcome.Succeeded)
        {
            UpdateState(() => sourceError = outcome.Error);
            throw new InvalidOperationException(outcome.Error?.Message ?? "winget source failed.");
        }

        UpdateState(() =>
        {
            sourceError = null;
            if (updateRows)
            {
                sources.Clear();
                sources.AddRange(outcome.Rows);
                ApplySourcePreferences();
            }
        });
    }

    private IReadOnlyList<string> GetEnabledSourceNames() => ReadState<IReadOnlyList<string>>(() =>
        sources.Where(source => source.IsEnabled)
            .Select(source => source.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray());

    private void RequireSourceElevation()
    {
        if (ReadState(() => capabilities.IsElevated) != true)
        {
            throw new InvalidOperationException("Restart OnlyWinget as administrator to add, remove or reset winget sources.");
        }
    }

    private void ApplySourcePreferences()
    {
        for (var index = 0; index < sources.Count; index++)
        {
            var source = sources[index];
            sources[index] = source with { IsEnabled = !disabledSources.Contains(source.Name) };
        }
    }

    private async Task EnsureOfficialSourcesConfiguredAsync(CancellationToken cancellationToken)
    {
        if (ReadState(() => defaultSourcesConfigured)) return;
        var listOutcome = await sourceService.ListSourcesAsync(cancellationToken).ConfigureAwait(false);
        ApplySourceOutcome(listOutcome, updateRows: true);

        var currentSources = listOutcome.Rows;
        var currentCapabilities = ReadState(() => capabilities);
        var isOlderOs = currentCapabilities.WindowsBuildNumber.HasValue && currentCapabilities.WindowsBuildNumber.Value < 19041;
        var isOlderWinget = false;
        if (!string.IsNullOrWhiteSpace(currentCapabilities.WingetVersion))
        {
            var verStr = currentCapabilities.WingetVersion.TrimStart('v').Split('-')[0];
            if (Version.TryParse(verStr, out var wingetVer) && wingetVer < new Version(1, 4))
            {
                isOlderWinget = true;
            }
        }

        var targetWingetUrl = (isOlderOs || isOlderWinget)
            ? "https://winget.azureedge.net/cache"
            : "https://cdn.winget.microsoft.com/cache";
        const string TargetMsStoreUrl = "https://storeedgefd.dsx.mp.microsoft.com/v9.0";

        var defaults = new[] { (Name: "winget", Url: targetWingetUrl), (Name: "msstore", Url: TargetMsStoreUrl) };
        var missing = defaults.Where(source => !currentSources.Any(existing =>
            string.Equals(existing.Name, source.Name, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (missing.Length > 0 && currentCapabilities.IsElevated != true)
        {
            AddActivity(ActivitySeverity.Warning, "Default source configuration requires administrator privileges",
                "Restart OnlyWinget as administrator to add missing default sources. Existing sources remain available.");
            return;
        }
        foreach (var source in missing)
        {
            var outcome = await sourceService.AddSourceAsync(source.Name, source.Url, cancellationToken).ConfigureAwait(false);
            ApplySourceOutcome(outcome, updateRows: false);
            AddActivity(ActivitySeverity.Information, "Source added", $"{source.Name}: {source.Url}");
        }
        await sourcePreferences.SaveAsync(
            ReadState(() => new SourcePreferences(disabledSources.ToArray(), DefaultSourcesConfigured: true)),
            cancellationToken).ConfigureAwait(false);
        UpdateState(() => defaultSourcesConfigured = true);
    }
}
