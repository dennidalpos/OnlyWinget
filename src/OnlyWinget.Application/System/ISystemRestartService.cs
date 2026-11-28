namespace OnlyWinget.Application.System;

/// <summary>Schedules or cancels a Windows restart (shutdown.exe /r). Only ever invoked by an explicit user action.</summary>
public interface ISystemRestartService
{
    /// <summary>True when Windows reports a pending restart (Windows Update or component servicing).</summary>
    bool IsRestartPending();

    /// <summary>Restarts after <paramref name="delay"/>; the user can abort with <see cref="CancelRestartAsync"/>.</summary>
    Task<bool> ScheduleRestartAsync(TimeSpan delay, CancellationToken cancellationToken);

    Task<bool> CancelRestartAsync(CancellationToken cancellationToken);
}
