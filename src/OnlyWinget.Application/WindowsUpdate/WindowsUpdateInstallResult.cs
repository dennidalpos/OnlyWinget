namespace OnlyWinget.Application.WindowsUpdate;

public sealed record WindowsUpdateInstallResult(
    WindowsUpdateIdentity Identity,
    string Title,
    bool Succeeded,
    bool RebootRequired,
    string ResultCode,
    string? Message);
