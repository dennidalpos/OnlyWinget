namespace OnlyWinget.Application.System;

public interface ISystemCapabilityService
{
    Task<SystemCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken);
}
