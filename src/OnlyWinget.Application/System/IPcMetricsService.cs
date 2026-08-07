namespace OnlyWinget.Application.System;

public sealed record PcMetrics(
    double CpuUsagePercent,
    double RamUsagePercent,
    string RamUsageText,
    double DiskUsagePercent,
    string DiskUsageText,
    string UptimeText,
    string OsVersionText,
    string NetworkStatusText
);

public interface IPcMetricsService
{
    PcMetrics GetCurrentMetrics();
}
