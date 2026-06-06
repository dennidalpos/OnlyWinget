// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

namespace OnlyWinget.Models;

public enum PackageOperationKind
{
    Install,
    Uninstall,
    Upgrade,
    UpdateWinget,
    SourceUpdate
}

public enum PackageOperationOutcome
{
    Succeeded,
    Failed,
    AlreadyInstalled,
    AlreadyUpdated,
    NoApplicableInstaller,
    NoApplicableUpgrade,
    AdvertisedUpdateNotApplied,
    StillAvailable,
    AdvancedArgumentsReviewRequired,
    PackageUnresolved,
    PackageAmbiguous,
    Cancelled,
    TimedOut
}

public enum PackageOperationExecutionMode
{
    Direct,
    Elevated
}

public sealed class PackageOperationRequest
{
    public PackageOperationKind Kind { get; init; }
    public string OperationKey { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Id { get; init; } = string.Empty;
    public string Source { get; init; } = AppEntry.DefaultSource;
    public string Version { get; init; } = string.Empty;
    public string AvailableVersion { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public string InstallMode { get; init; } = InstallModes.SilentWithProgress;
    public string Architecture { get; init; } = string.Empty;
    public string Locale { get; init; } = string.Empty;
    public string InstallerType { get; init; } = string.Empty;
    public string InstallLocation { get; init; } = string.Empty;
    public string LogPath { get; init; } = string.Empty;
    public bool SupportsInstallLocation { get; init; } = true;
    public bool SupportsLog { get; init; } = true;
    public string AdditionalCustomArgs { get; init; } = string.Empty;
    public string OverrideArgs { get; init; } = string.Empty;
    public bool AdvancedArgumentsReviewed { get; init; } = true;
    public string ElevationRequirement { get; init; } = string.Empty;

    public bool HasAdvancedArguments =>
        !string.IsNullOrWhiteSpace(AdditionalCustomArgs)
        || !string.IsNullOrWhiteSpace(OverrideArgs);

    public bool RequiresAdvancedArgumentsReview => HasAdvancedArguments && !AdvancedArgumentsReviewed;

    public static PackageOperationRequest FromAppEntry(AppEntry app)
    {
        var kind = string.Equals(app.Action, AppActions.Uninstall, System.StringComparison.Ordinal)
            ? PackageOperationKind.Uninstall
            : PackageOperationKind.Install;

        return new PackageOperationRequest
        {
            Kind = kind,
            OperationKey = app.OperationKey,
            Name = app.Name,
            Id = app.Id,
            Source = AppEntry.NormalizeSource(app.Source),
            Scope = app.Scope,
            InstallMode = app.InstallMode,
            Architecture = app.Architecture,
            Locale = app.Locale,
            InstallerType = app.InstallerType,
            InstallLocation = app.InstallLocation,
            LogPath = app.LogPath,
            SupportsInstallLocation = app.SupportsInstallLocation,
            SupportsLog = app.SupportsLog,
            AdditionalCustomArgs = app.AdditionalCustomArgs,
            OverrideArgs = app.OverrideArgs,
            AdvancedArgumentsReviewed = app.AdvancedArgumentsReviewed,
            ElevationRequirement = app.ElevationRequirement
        };
    }

    public static PackageOperationRequest FromUpdateEntry(UpdateEntry update)
    {
        return new PackageOperationRequest
        {
            Kind = PackageOperationKind.Upgrade,
            OperationKey = update.Id,
            Name = update.Name,
            Id = update.Id,
            Source = AppEntry.NormalizeSource(update.Source),
            Version = update.Version,
            AvailableVersion = update.Available,
            Scope = update.Scope,
            Architecture = update.Architecture,
            Locale = update.Locale,
            InstallerType = update.InstallerType
        };
    }

    public static PackageOperationRequest ForUpdateWinget()
    {
        return new PackageOperationRequest
        {
            Kind = PackageOperationKind.UpdateWinget,
            OperationKey = "Microsoft.AppInstaller",
            Name = "Microsoft App Installer",
            Id = "Microsoft.AppInstaller",
            Source = AppEntry.DefaultSource
        };
    }

    public static PackageOperationRequest ForSourceUpdate()
    {
        return new PackageOperationRequest
        {
            Kind = PackageOperationKind.SourceUpdate,
            OperationKey = "winget-source-update",
            Name = "winget sources",
            Id = "winget-source-update",
            Source = AppEntry.DefaultSource
        };
    }

    public AppEntry ToAppEntry()
    {
        return new AppEntry
        {
            Name = Name,
            Id = Id,
            Source = Source,
            Action = Kind == PackageOperationKind.Uninstall ? AppActions.Uninstall : AppActions.Install,
            Scope = Scope,
            InstallMode = InstallMode,
            Architecture = Architecture,
            Locale = Locale,
            InstallerType = InstallerType,
            InstallLocation = InstallLocation,
            LogPath = LogPath,
            SupportsInstallLocation = SupportsInstallLocation,
            SupportsLog = SupportsLog,
            AdditionalCustomArgs = AdditionalCustomArgs,
            OverrideArgs = OverrideArgs,
            AdvancedArgumentsReviewed = AdvancedArgumentsReviewed,
            ElevationRequirement = ElevationRequirement
        };
    }

    public PackageOperationRequest WithResolvedPackage(SavedPackageResolutionResult resolution)
    {
        return new PackageOperationRequest
        {
            Kind = Kind,
            OperationKey = OperationKey,
            Name = string.IsNullOrWhiteSpace(resolution.Name) ? Name : resolution.Name,
            Id = resolution.Id,
            Source = AppEntry.NormalizeSource(resolution.Source),
            Version = Version,
            AvailableVersion = AvailableVersion,
            Scope = Scope,
            InstallMode = InstallMode,
            Architecture = Architecture,
            Locale = Locale,
            InstallerType = InstallerType,
            InstallLocation = InstallLocation,
            LogPath = LogPath,
            SupportsInstallLocation = SupportsInstallLocation,
            SupportsLog = SupportsLog,
            AdditionalCustomArgs = AdditionalCustomArgs,
            OverrideArgs = OverrideArgs,
            AdvancedArgumentsReviewed = AdvancedArgumentsReviewed,
            ElevationRequirement = ElevationRequirement
        };
    }
}

public sealed class PackageOperationResult
{
    public PackageOperationKind Kind { get; init; }
    public PackageOperationOutcome Outcome { get; init; }
    public string OperationKey { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Id { get; init; } = string.Empty;
    public string Source { get; init; } = AppEntry.DefaultSource;
    public int ExitCode { get; init; }
    public string Output { get; init; } = string.Empty;
    public bool AppendOutput { get; init; } = true;
    public string Message { get; init; } = string.Empty;
    public string Resolution { get; init; } = string.Empty;
    public string RedactedCommand { get; init; } = string.Empty;
    public string LogPath { get; init; } = string.Empty;
    public PackageOperationExecutionMode ExecutionMode { get; init; } = PackageOperationExecutionMode.Direct;
    public IReadOnlyList<string> DiagnosticEvents { get; init; } = System.Array.Empty<string>();
}
