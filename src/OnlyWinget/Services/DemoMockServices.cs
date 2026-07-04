using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OnlyWinget.Application.System;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Operations;
using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Services;

public sealed class DemoSystemCapabilityService : ISystemCapabilityService
{
    public Task<SystemCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new SystemCapabilities(true, true, true, true, null));
}

public sealed class DemoWindowsUpdateService : IWindowsUpdateService
{
    public Task<WindowsUpdateOperationOutcome<WindowsUpdateItem>> ScanAsync(
        WindowsUpdateOptions options,
        CancellationToken cancellationToken)
    {
        var list = new List<WindowsUpdateItem>
        {
            new WindowsUpdateItem(
                new WindowsUpdateIdentity("KB5040442", 1),
                "Cumulative Update for Windows 10 Version 22H2 (KB5040442)",
                "A security update has been released that resolves vulnerabilities in Microsoft Windows.",
                "Critical",
                new[] { "Security Updates" },
                new[] { "KB5040442" },
                1024 * 1024 * 145,
                false,
                false),
            new WindowsUpdateItem(
                new WindowsUpdateIdentity("KB5040226", 1),
                "Security Update for .NET Framework 3.5 and 4.8.1 (KB5040226)",
                "Security issues have been identified in .NET Framework that could allow an attacker to compromise your system.",
                "Important",
                new[] { "Security Updates" },
                new[] { "KB5040226" },
                1024 * 1024 * 34,
                false,
                false)
        };
        return Task.FromResult(WindowsUpdateOperationOutcome<WindowsUpdateItem>.Success(list, "Demo Windows Update Scan Successful"));
    }

    public Task<WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>> InstallAsync(
        IReadOnlyList<WindowsUpdateIdentity> updates,
        WindowsUpdateOptions options,
        CancellationToken cancellationToken)
    {
        var results = updates.Select(u => new WindowsUpdateInstallResult(u, "Mock Update", true, false, "0x00000000", null)).ToList();
        return Task.FromResult(WindowsUpdateOperationOutcome<WindowsUpdateInstallResult>.Success(results, "Demo Windows Update Install Successful"));
    }
}

public sealed class DemoOperationExecutor : IOperationExecutor
{
    public async Task<OperationExecutionSummary> ExecuteAsync(
        OperationPlan plan,
        CancellationToken cancellationToken,
        IProgress<OperationProgress>? progress = null,
        bool continueAfterFailure = false)
    {
        int index = 0;
        foreach (var selection in plan.Selections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (int pct = 0; pct <= 100; pct += 25)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new OperationProgress(
                    selection.Package.Id,
                    WingetProgressPhase.Installing,
                    pct,
                    index,
                    plan.Selections.Count));
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
            index++;
        }

        var results = plan.Selections.Select(selection => new OperationExecutionResult(
            selection,
            new WingetCommandResult(0, "Demo Success Output", string.Empty),
            null
        )).ToList();

        return new OperationExecutionSummary(results);
    }
}

public sealed class DemoPackageResolver : IPackageResolver
{
    public Task<PackageResolution> ResolveAsync(PackageIdentity package, CancellationToken cancellationToken)
    {
        var name = package.Id.Split('.').Last();
        return Task.FromResult(new PackageResolution(package, name, "1.0.0", "Demo Publisher", true, null, new[] { "x64" }));
    }
}

public sealed class DemoPackageSearchService : IPackageSearchService
{
    private static readonly List<PackageSearchResult> MockRegistry = new()
    {
        new PackageSearchResult(new PackageIdentity("Microsoft.VisualStudioCode", "winget"), "VS Code", "1.91.0", "vs code"),
        new PackageSearchResult(new PackageIdentity("Git.Git", "winget"), "Git", "2.45.2", "git"),
        new PackageSearchResult(new PackageIdentity("Google.Chrome", "winget"), "Google Chrome", "126.0.0", "chrome"),
        new PackageSearchResult(new PackageIdentity("Microsoft.PowerToys", "winget"), "PowerToys", "0.82.0", "powertoys"),
        new PackageSearchResult(new PackageIdentity("VideoLAN.VLC", "winget"), "VLC Media Player", "3.0.21", "vlc"),
        new PackageSearchResult(new PackageIdentity("7zip.7zip", "winget"), "7-Zip", "24.07", "7zip")
    };

    public Task<WingetOperationOutcome<PackageSearchResult>> SearchAsync(
        PackageSearchRequest request,
        CancellationToken cancellationToken)
    {
        var query = request.Query?.Trim() ?? string.Empty;
        var filtered = MockRegistry
            .Where(r => r.Package.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        r.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult(WingetOperationOutcome<PackageSearchResult>.Success(filtered, "Demo Search Successful"));
    }
}

public sealed class DemoUpdateLoader : IUpdateLoader
{
    public Task<WingetOperationOutcome<PackageUpdate>> LoadUpdatesAsync(string source, CancellationToken cancellationToken)
    {
        var updates = new List<PackageUpdate>
        {
            new PackageUpdate(new PackageIdentity("Microsoft.VisualStudioCode", source), "VS Code", "1.90.0", "1.91.0"),
            new PackageUpdate(new PackageIdentity("Git.Git", source), "Git", "2.44.0", "2.45.2"),
            new PackageUpdate(new PackageIdentity("Google.Chrome", source), "Google Chrome", "125.0.0", "126.0.0")
        };
        return Task.FromResult(WingetOperationOutcome<PackageUpdate>.Success(updates, "Demo Updates Loaded"));
    }
}

public sealed class DemoWingetSourceService : IWingetSourceService
{
    private readonly List<WingetSource> sources = new()
    {
        new WingetSource("winget", "https://cdn.winget.microsoft.com/cache", false, WingetSourceStatus.Available),
        new WingetSource("msstore", "https://storeedgefd.dsx.mp.microsoft.com/v2.0", false, WingetSourceStatus.Available)
    };

    public Task<WingetOperationOutcome<WingetSource>> ListSourcesAsync(CancellationToken cancellationToken) =>
        Task.FromResult(WingetOperationOutcome<WingetSource>.Success(sources, "Demo Sources Listed"));

    public Task<WingetOperationOutcome<WingetSource>> UpdateSourcesAsync(CancellationToken cancellationToken) =>
        Task.FromResult(WingetOperationOutcome<WingetSource>.Success(sources, "Demo Sources Updated"));

    public Task<WingetOperationOutcome<WingetSource>> AddSourceAsync(
        string name,
        string argument,
        CancellationToken cancellationToken)
    {
        if (sources.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(WingetOperationOutcome<WingetSource>.Failure(
                new ClassifiedWingetError(WingetErrorKind.Unknown, "Source already exists"),
                "Demo Error"
            ));
        }
        var newSource = new WingetSource(name, argument, true, WingetSourceStatus.Available);
        sources.Add(newSource);
        return Task.FromResult(WingetOperationOutcome<WingetSource>.Success(sources, "Demo Source Added"));
    }

    public Task<WingetOperationOutcome<WingetSource>> RemoveSourceAsync(
        string name,
        CancellationToken cancellationToken)
    {
        var source = sources.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        if (source is not null)
        {
            sources.Remove(source);
        }
        return Task.FromResult(WingetOperationOutcome<WingetSource>.Success(sources, "Demo Source Removed"));
    }

    public Task<WingetOperationOutcome<WingetSource>> ResetSourcesAsync(CancellationToken cancellationToken)
    {
        sources.Clear();
        sources.Add(new WingetSource("winget", "https://cdn.winget.microsoft.com/cache", false, WingetSourceStatus.Available));
        sources.Add(new WingetSource("msstore", "https://storeedgefd.dsx.mp.microsoft.com/v2.0", false, WingetSourceStatus.Available));
        return Task.FromResult(WingetOperationOutcome<WingetSource>.Success(sources, "Demo Sources Reset"));
    }
}
