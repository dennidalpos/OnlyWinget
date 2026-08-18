using System.Reflection;
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

    // Regression test: the CPU-delta bookkeeping fields (lastIdleTime/lastTotalTime) used to be
    // `static`, so every PcMetricsService instance shared the same counters even though the service
    // is registered as a singleton (harmless today, but fragile for any future second instance, e.g.
    // in tests). They must be per-instance fields now.
    [Fact]
    public void CpuDeltaFieldsAreInstanceStateNotShared()
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        Assert.NotNull(typeof(PcMetricsService).GetField("lastIdleTime", flags));
        Assert.NotNull(typeof(PcMetricsService).GetField("lastTotalTime", flags));

        var staticFlags = BindingFlags.NonPublic | BindingFlags.Static;
        Assert.Null(typeof(PcMetricsService).GetField("lastIdleTime", staticFlags));
        Assert.Null(typeof(PcMetricsService).GetField("lastTotalTime", staticFlags));
    }
}
