namespace OnlyWinget.Application.WindowsUpdate;

public sealed record WindowsUpdateOptions(
    bool IncludeSoftware = true,
    bool IncludeDrivers = true,
    bool IncludeMicrosoftUpdates = false,
    bool IncludePotentiallySupersededUpdates = false);
