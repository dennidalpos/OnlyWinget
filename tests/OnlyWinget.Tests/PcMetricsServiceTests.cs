using OnlyWinget.Infrastructure.System;
using Xunit;

namespace OnlyWinget.Tests;

public class PcMetricsServiceTests
{
    [Fact]
    public void GetCurrentMetrics_ReturnsValidMetrics()
    {
        var service = new PcMetricsService();
        var metrics = service.GetCurrentMetrics();

        Assert.NotNull(metrics);
        Assert.InRange(metrics.CpuUsagePercent, 0.0, 100.0);
        Assert.True(metrics.RamUsagePercent >= 0.0);
        Assert.False(string.IsNullOrWhiteSpace(metrics.RamUsageText));
        Assert.True(metrics.DiskUsagePercent >= 0.0);
        Assert.False(string.IsNullOrWhiteSpace(metrics.DiskUsageText));
        Assert.False(string.IsNullOrWhiteSpace(metrics.UptimeText));
        Assert.False(string.IsNullOrWhiteSpace(metrics.OsVersionText));
        Assert.False(string.IsNullOrWhiteSpace(metrics.NetworkStatusText));
    }
}
