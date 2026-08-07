using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using OnlyWinget.Application.System;

namespace OnlyWinget.Infrastructure.System;

public sealed class PcMetricsService : IPcMetricsService
{
    private static long lastIdleTime;
    private static long lastTotalTime;

    public PcMetrics GetCurrentMetrics()
    {
        var cpuUsage = GetCpuUsage();
        var (ramPercent, ramText) = GetRamInfo();
        var (diskPercent, diskText) = GetDiskInfo();
        var uptimeText = GetUptimeText();
        var osVersionText = GetOsVersionText();
        var networkStatusText = GetNetworkStatusText();

        return new PcMetrics(
            cpuUsage,
            ramPercent,
            ramText,
            diskPercent,
            diskText,
            uptimeText,
            osVersionText,
            networkStatusText);
    }

    private static double GetCpuUsage()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                GetSystemTimes(out var idleFt, out var kernelFt, out var userFt))
            {
                var idleTime = FileTimeToLong(idleFt);
                var totalTime = FileTimeToLong(kernelFt) + FileTimeToLong(userFt);

                var idleDelta = idleTime - lastIdleTime;
                var totalDelta = totalTime - lastTotalTime;

                lastIdleTime = idleTime;
                lastTotalTime = totalTime;

                if (totalDelta > 0 && lastTotalTime > 0)
                {
                    var cpu = 100.0 * (1.0 - ((double)idleDelta / totalDelta));
                    return Math.Clamp(Math.Round(cpu, 1), 0.0, 100.0);
                }
            }
        }
        catch
        {
        }
        return 0.0;
    }

    private static (double percent, string text) GetRamInfo()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                {
                    var totalGb = memStatus.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                    var availGb = memStatus.ullAvailPhys / (1024.0 * 1024.0 * 1024.0);
                    var usedGb = totalGb - availGb;
                    var loadPct = memStatus.dwMemoryLoad;
                    return (loadPct, $"{usedGb:F1} GB / {totalGb:F1} GB ({loadPct}%)");
                }
            }
        }
        catch
        {
        }
        return (0, "N/A");
    }

    private static (double percent, string text) GetDiskInfo()
    {
        try
        {
            var systemDrivePath = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var drive = new DriveInfo(systemDrivePath);
            if (drive.IsReady)
            {
                var totalGb = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                var freeGb = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                var usedGb = totalGb - freeGb;
                var pct = totalGb > 0 ? (usedGb / totalGb) * 100.0 : 0.0;
                return (Math.Round(pct, 1), $"{freeGb:F1} GB free / {totalGb:F1} GB");
            }
        }
        catch
        {
        }
        return (0, "N/A");
    }

    private static string GetUptimeText()
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        return uptime.Days > 0
            ? $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m"
            : $"{uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
    }

    private static string GetOsVersionText()
    {
        return RuntimeInformation.OSDescription;
    }

    private static string GetNetworkStatusText()
    {
        try
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                return "Disconnected";
            }

            var activeInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
                              nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                              nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .ToList();

            if (activeInterfaces.Any(nic => nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
            {
                return "Connected (Wi-Fi)";
            }
            if (activeInterfaces.Any(nic => nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet))
            {
                return "Connected (Ethernet)";
            }
            return activeInterfaces.Count > 0 ? "Connected" : "Disconnected";
        }
        catch
        {
            return "Unknown";
        }
    }

    private static long FileTimeToLong(global::System.Runtime.InteropServices.ComTypes.FILETIME ft)
    {
        return ((long)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(
        out global::System.Runtime.InteropServices.ComTypes.FILETIME lpIdleTime,
        out global::System.Runtime.InteropServices.ComTypes.FILETIME lpKernelTime,
        out global::System.Runtime.InteropServices.ComTypes.FILETIME lpUserTime);
}
