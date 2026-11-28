namespace OnlyWinget.Application.System;

public enum PcNetworkStatus
{
    Unknown,
    Disconnected,
    Connected,
    ConnectedWifi,
    ConnectedEthernet
}

public sealed record PcMetrics(
    double CpuUsagePercent,
    double RamUsagePercent,
    double? RamUsedGb,
    double? RamTotalGb,
    double DiskUsagePercent,
    double? DiskFreeGb,
    double? DiskTotalGb,
    TimeSpan Uptime,
    string OsVersionText,
    PcNetworkStatus NetworkStatus
);

public interface IPcMetricsService
{
    PcMetrics GetCurrentMetrics();
}
