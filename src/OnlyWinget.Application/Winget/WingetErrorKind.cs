namespace OnlyWinget.Application.Winget;

public enum WingetErrorKind
{
    None,
    NotFound,
    NoUpdates,
    SourceUnavailable,
    Cancelled,
    CannotUpgrade,
    HashMismatch,
    Unknown,
    InvalidOutput,

    /// <summary>The installer finished but a restart is needed (0x8A150109) or was initiated (0x8A15010B).</summary>
    RebootRequired,

    /// <summary>The package is already installed (0x8A15010D, 0x8A150061).</summary>
    AlreadyInstalled,

    /// <summary>The package is pinned (0x8A150068).</summary>
    Pinned,

    /// <summary>The package or one of its files is in use (0x8A150101, 0x8A150103, 0x8A150111).</summary>
    InUse,

    /// <summary>No installer applies to this system (0x8A150010).</summary>
    NoApplicableInstaller,

    /// <summary>A policy blocks the operation (0x8A15003A, 0x8A15010F).</summary>
    PolicyBlocked,

    /// <summary>The installer needs a network connection (0x8A150107).</summary>
    Network,

    /// <summary>The disk is full (0x8A150105).</summary>
    DiskFull,

    /// <summary>The Microsoft Store source has no applicable package, often a region or availability issue (0x8A150080, 0x8A150082).</summary>
    StoreUnavailable,

    /// <summary>A newer version is already installed (0x8A15010E).</summary>
    Downgrade,

    /// <summary>Another installation is in progress (0x8A150102); the only installer-side error that is safe to retry.</summary>
    InstallInProgress,

    /// <summary>A restart is required before this install can run (0x8A15010A).</summary>
    RebootRequiredFirst
}

public static class WingetErrorKindExtensions
{
    /// <summary>
    /// True when the kind represents a result the user does not need to fix: nothing to upgrade, already installed,
    /// or installed with a restart pending.
    /// </summary>
    public static bool IsSuccessfulOutcome(this WingetErrorKind kind) =>
        kind is WingetErrorKind.NoUpdates or WingetErrorKind.AlreadyInstalled or WingetErrorKind.RebootRequired;
}
