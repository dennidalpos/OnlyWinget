using OnlyWinget.Application.Activity;
using OnlyWinget.Application.Storage;
using OnlyWinget.Application.Winget;

namespace OnlyWinget.Application.App;

public sealed partial class OnlyWingetApplication
{
    public async Task<ApplicationActionResult> RefreshSourcesAsync(CancellationToken cancellationToken)
    {
        return await RunAsync(
                ApplicationBusyState.ManagingSources,
                async () =>
                {
                    RequireWinget();
                    await EnsureOfficialSourcesConfiguredAsync(cancellationToken).ConfigureAwait(false);
                    var outcome = await sourceService.ListSourcesAsync(cancellationToken).ConfigureAwait(false);
                    ApplySourceOutcome(outcome, updateRows: true);
                    AddActivity(ActivitySeverity.Information, "Sources refreshed", $"{sources.Count} source(s).");
                },
                "Unable to refresh winget sources.")
            .ConfigureAwait(false);
    }

    public async Task<ApplicationActionResult> UpdateSourcesAsync(CancellationToken cancellationToken)
    {
        return await RunSourceMutationAsync(
                () => sourceService.UpdateSourcesAsync(cancellationToken),
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
                () => sourceService.AddSourceAsync(name, argument, cancellationToken),
                "Source added",
                name,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ApplicationActionResult> RemoveSourceAsync(string name, CancellationToken cancellationToken)
    {
        return await RunSourceMutationAsync(
                () => sourceService.RemoveSourceAsync(name, cancellationToken),
                "Source removed",
                name,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ApplicationActionResult> ResetSourcesAsync(CancellationToken cancellationToken)
    {
        var result = await RunSourceMutationAsync(
                () => sourceService.ResetSourcesAsync(cancellationToken),
                "Sources reset",
                "winget sources reset to defaults.",
                cancellationToken)
            .ConfigureAwait(false);
        if (result.Succeeded)
        {
            disabledSources.Clear();
            defaultSourcesConfigured = true;
            ApplySourcePreferences();
            await sourcePreferences.SaveAsync(new SourcePreferences([], DefaultSourcesConfigured: true), cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    public async Task<ApplicationActionResult> SetSourceEnabledAsync(
        string name,
        bool isEnabled,
        CancellationToken cancellationToken)
    {
        return await RunAsync(
                ApplicationBusyState.ManagingSources,
                async () =>
                {
                    RequireWinget();
                    if (!sources.Any(source => string.Equals(source.Name, name, StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new InvalidOperationException("The winget source was not found.");
                    }

                    if (isEnabled)
                    {
                        disabledSources.Remove(name);
                    }
                    else
                    {
                        disabledSources.Add(name);
                    }

                    await sourcePreferences.SaveAsync(
                            new SourcePreferences(disabledSources.ToArray()),
                            cancellationToken)
                        .ConfigureAwait(false);
                    ApplySourcePreferences();
                    AddActivity(ActivitySeverity.Information, "Source preference changed", $"{name}: {(isEnabled ? "enabled" : "disabled")}");
                },
                "Unable to save the source preference.")
            .ConfigureAwait(false);
    }

    private async Task<ApplicationActionResult> RunSourceMutationAsync(
        Func<Task<WingetOperationOutcome<WingetSource>>> operation,
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        return await RunAsync(
                ApplicationBusyState.ManagingSources,
                async () =>
                {
                    RequireWinget();
                    var outcome = await operation().ConfigureAwait(false);
                    ApplySourceOutcome(outcome, updateRows: false);
                    AddActivity(ActivitySeverity.Success, title, message);

                    var refresh = await sourceService.ListSourcesAsync(cancellationToken).ConfigureAwait(false);
                    ApplySourceOutcome(refresh, updateRows: true);
                    await sourcePreferences.SaveAsync(
                            new SourcePreferences(disabledSources.ToArray(), defaultSourcesConfigured),
                            cancellationToken)
                        .ConfigureAwait(false);
                },
                "Unable to manage winget sources.")
            .ConfigureAwait(false);
    }

    private void ApplySourceOutcome(WingetOperationOutcome<WingetSource> outcome, bool updateRows)
    {
        if (!outcome.Succeeded)
        {
            sourceError = outcome.Error;
            throw new InvalidOperationException(outcome.Error?.Message ?? "winget source failed.");
        }

        sourceError = null;
        if (updateRows)
        {
            sources.Clear();
            sources.AddRange(outcome.Rows);
            ReconcileSourcePreferences();
            ApplySourcePreferences();
        }
    }

    private IReadOnlyList<string> GetEnabledSourceNames() =>
        sources.Where(source => source.IsEnabled)
            .Select(source => source.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private void ReconcileSourcePreferences()
    {
        var currentNames = sources.Select(source => source.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        disabledSources.RemoveWhere(name => !currentNames.Contains(name));
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
        var listOutcome = await sourceService.ListSourcesAsync(cancellationToken).ConfigureAwait(false);
        if (!listOutcome.Succeeded)
        {
            return;
        }

        var currentSources = listOutcome.Rows;
        var isOlderOs = capabilities.WindowsBuildNumber.HasValue && capabilities.WindowsBuildNumber.Value < 19041;
        var isOlderWinget = false;
        if (!string.IsNullOrWhiteSpace(capabilities.WingetVersion))
        {
            var verStr = capabilities.WingetVersion.TrimStart('v').Split('-')[0];
            if (Version.TryParse(verStr, out var wingetVer))
            {
                if (wingetVer < new Version(1, 4))
                {
                    isOlderWinget = true;
                }
            }
        }

        var targetWingetUrl = (isOlderOs || isOlderWinget)
            ? "https://winget.azureedge.net/cache"
            : "https://cdn.winget.microsoft.com/cache";

        var targetMsStoreUrl = "https://storeedgefd.dsx.mp.microsoft.com/v9.0";

        // Verify and update "winget" source
        var wingetSource = currentSources.FirstOrDefault(s => string.Equals(s.Name, "winget", StringComparison.OrdinalIgnoreCase));
        if (wingetSource == null)
        {
            await sourceService.AddSourceAsync("winget", targetWingetUrl, cancellationToken).ConfigureAwait(false);
        }
        else if (!string.Equals(wingetSource.Argument.Trim().TrimEnd('/'), targetWingetUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
        {
            var removeOutcome = await sourceService.RemoveSourceAsync("winget", cancellationToken).ConfigureAwait(false);
            if (removeOutcome.Succeeded)
            {
                await sourceService.AddSourceAsync("winget", targetWingetUrl, cancellationToken).ConfigureAwait(false);
            }
        }

        // Verify and update "msstore" source
        var msstoreSource = currentSources.FirstOrDefault(s => string.Equals(s.Name, "msstore", StringComparison.OrdinalIgnoreCase));
        if (msstoreSource == null)
        {
            await sourceService.AddSourceAsync("msstore", targetMsStoreUrl, cancellationToken).ConfigureAwait(false);
        }
        else if (!string.Equals(msstoreSource.Argument.Trim().TrimEnd('/'), targetMsStoreUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
        {
            var removeOutcome = await sourceService.RemoveSourceAsync("msstore", cancellationToken).ConfigureAwait(false);
            if (removeOutcome.Succeeded)
            {
                await sourceService.AddSourceAsync("msstore", targetMsStoreUrl, cancellationToken).ConfigureAwait(false);
            }
        }

        // Ensure default sources are enabled (active)
        var preferencesChanged = false;
        if (disabledSources.Contains("winget"))
        {
            disabledSources.Remove("winget");
            preferencesChanged = true;
        }
        if (disabledSources.Contains("msstore"))
        {
            disabledSources.Remove("msstore");
            preferencesChanged = true;
        }

        if (preferencesChanged || !defaultSourcesConfigured)
        {
            defaultSourcesConfigured = true;
            await sourcePreferences.SaveAsync(
                new SourcePreferences(disabledSources.ToArray(), DefaultSourcesConfigured: true),
                cancellationToken).ConfigureAwait(false);
        }
    }
}
