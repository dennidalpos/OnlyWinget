namespace OnlyWinget.Application.WindowsUpdate;

public sealed record WindowsUpdateOptions(
    bool IncludeSoftware = true,
    bool IncludeDrivers = false,
    bool IncludeMicrosoftUpdates = false,
    bool IncludePotentiallySupersededUpdates = false);
